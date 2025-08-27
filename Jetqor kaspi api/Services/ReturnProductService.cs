using System.Text.Json;
using Jetqor_kaspi_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;

namespace Jetqor_kaspi_api.Services;

public class ReturnProductService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;

    public ReturnProductService(IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
    }

    public async Task ReturnProduct(string orderId, string authToken, int userId)
    {
        var client = _httpClientFactory.CreateClient();

        var entriesUrl = $"https://kaspi.kz/shop/api/v2/orders/{orderId}/entries";

        using var request = new HttpRequestMessage(HttpMethod.Get, entriesUrl);

        request.Headers.Add("X-Auth-Token", authToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

        request.Content = new StringContent(string.Empty);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var entriesJson = await response.Content.ReadAsStringAsync();

        Console.WriteLine("===== Kaspi API response =====");
        Console.WriteLine(entriesJson);
        Console.WriteLine("================================");

        var entriesData = JsonSerializer.Deserialize<JsonElement>(entriesJson);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.kaspi_key == authToken);
        if (user == null)
            throw new Exception("User not found by token");

        if (!entriesData.TryGetProperty("data", out var dataArray))
            throw new Exception("No 'data' property in Kaspi API response");

        foreach (var entry in dataArray.EnumerateArray())
        {
            var attributes = entry.GetProperty("attributes");
            var quantity = attributes.GetProperty("quantity").GetInt32();

            string? offerName = null;
            if (attributes.TryGetProperty("offer", out var offer))
            {
                offerName = offer.GetProperty("name").GetString();
            }

            if (string.IsNullOrEmpty(offerName))
            {
                Console.WriteLine("Offer name not found in JSON. Skipping entry.");
                continue;
            }

            Console.WriteLine($"Parsed offer name: {offerName}");

            var product = await db.Products.FirstOrDefaultAsync(p => p.name == offerName);
            if (product == null)
            {
                Console.WriteLine($"Product not found in DB for name: {offerName}");
                continue;
            }

            var entity = new Entity
            {
                count = quantity,
                productId = product.id,
                cellId = 37,  
                ownerId = userId,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };

            db.Entities.Add(entity);
        }

        await db.SaveChangesAsync();
    }
}
