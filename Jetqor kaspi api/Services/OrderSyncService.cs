using System.Text.Json;
using Jetqor_kaspi_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Jetqor_kaspi_api.Services;

public class OrderSyncService
{
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProductSyncService _productSyncService;

    public OrderSyncService(HttpClient httpClient, IServiceScopeFactory scopeFactory, ProductSyncService productSyncService)
    {
        _httpClient = httpClient;
        _scopeFactory = scopeFactory;
        _productSyncService = productSyncService;
        _httpClient.DefaultRequestHeaders.Clear();
    }

    public async Task SyncOrderAsync(string kaspiCode, string authToken)
    {
        var orderUrl = $"https://kaspi.kz/shop/api/v2/orders?filter[orders][code]={kaspiCode}";
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", authToken);
        _httpClient.DefaultRequestHeaders.Add("ContentType", "application/vnd.api+json");

        var orderResponse = await _httpClient.GetAsync(orderUrl);
        orderResponse.EnsureSuccessStatusCode();
        var orderJson = await orderResponse.Content.ReadAsStringAsync();
        var orderData = JsonSerializer.Deserialize<JsonElement>(orderJson);

        string orderId = orderData.GetProperty("data")[0].GetProperty("id").GetString();

        var entriesUrl = $"https://kaspi.kz/shop/api/v2/orders/{orderId}/entries";
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", authToken);
        _httpClient.DefaultRequestHeaders.Add("ContentType", "application/vnd.api+json");

        var entriesResponse = await _httpClient.GetAsync(entriesUrl);
        entriesResponse.EnsureSuccessStatusCode();
        var entriesJson = await entriesResponse.Content.ReadAsStringAsync();
        var entriesData = JsonSerializer.Deserialize<JsonElement>(entriesJson);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        //fix line
        var order = await context.Orders.FirstOrDefaultAsync(o => o.kaspi_code == kaspiCode);
        
        if (!entriesData.TryGetProperty("data", out var entryArray) || entryArray.ValueKind != JsonValueKind.Array || entryArray.GetArrayLength() == 0)
        {
            Console.WriteLine($"[INFO] No entries found for order {kaspiCode}");
            return;
        }

        foreach (var entry in entriesData.GetProperty("data").EnumerateArray())
        {
            try
            {
                var attributes = entry.GetProperty("attributes");
                var offer = attributes.GetProperty("offer");
                var articleCode = offer.GetProperty("code").GetString();

                var product = await _productSyncService.SyncProductAsync(articleCode);
                var quantity = attributes.GetProperty("quantity").GetInt32();

                var existingOrderProduct = await context.OrderProducts
                    .FirstOrDefaultAsync(op => op.orderId == order.Id && op.productId == product.id);

                if (existingOrderProduct == null)
                {
                    var orderProduct = new OrderProduct
                    {
                        orderId = order.Id,
                        productId = product.id,
                        count = quantity
                    };

                    context.OrderProducts.Add(orderProduct);
                }
                else
                {
                    existingOrderProduct.count = quantity;
                    context.OrderProducts.Update(existingOrderProduct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }


        await context.SaveChangesAsync();
    }
}
