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

    public async Task SyncOrderAsync(string kaspiCode, string authToken, string id)
    {
        var entriesUrl = $"https://kaspi.kz/shop/api/v2/orders/{id}/entries";
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", authToken);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.api+json");

        var entriesResponse = await _httpClient.GetAsync(entriesUrl);
        entriesResponse.EnsureSuccessStatusCode();
        var entriesJson = await entriesResponse.Content.ReadAsStringAsync();
        var entriesData = JsonSerializer.Deserialize<JsonElement>(entriesJson);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var order = await context.Orders.FirstOrDefaultAsync(o => o.kaspi_code == kaspiCode);
        if (order == null)
        {
            Console.WriteLine($"[WARNING] Order {kaspiCode} not found in DB");
            return;
        }

        if (!entriesData.TryGetProperty("data", out var entryArray) || entryArray.ValueKind != JsonValueKind.Array || entryArray.GetArrayLength() == 0)
        {
            Console.WriteLine($"[INFO] No entries found for order {kaspiCode}");
            return;
        }

        bool hasProducts = false; 

        foreach (var entry in entriesData.GetProperty("data").EnumerateArray())
        {
            try
            {
                var attributes = entry.GetProperty("attributes");
                var offer = attributes.GetProperty("offer");
                var articleCode = offer.GetProperty("code").GetString();

                var product = await _productSyncService.SyncProductAsync(articleCode);
                if (product == null)
                {
                    Console.WriteLine($"[INFO] Product {articleCode} not found, skipping");
                    continue;
                }

                var quantity = attributes.GetProperty("quantity").GetInt32();

                var existingOrderProduct = await context.OrderProducts
                    .FirstOrDefaultAsync(op => op.orderId == order.Id && op.productId == product.id);

                if (existingOrderProduct == null)
                {
                    var orderProduct = new OrderProduct
                    {
                        orderId = order.Id,
                        productId = product.id,
                        count = quantity,
                    };

                    context.OrderProducts.Add(orderProduct);
                }
                else
                {
                    existingOrderProduct.count = quantity;
                    context.OrderProducts.Update(existingOrderProduct);
                }

                hasProducts = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to sync product for order {kaspiCode}: {ex.Message}");
            }
        }

        if (hasProducts)
        {
            await context.SaveChangesAsync();
            Console.WriteLine($"[INFO] Order {kaspiCode} synced successfully");
        }
        else
        {
            Console.WriteLine($"[INFO] Order {kaspiCode} skipped (no valid products)");
        }
    }
}
