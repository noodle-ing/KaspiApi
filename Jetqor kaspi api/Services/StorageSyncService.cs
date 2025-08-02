using System.Text.Json;
using Jetqor_kaspi_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Jetqor_kaspi_api.Services;

public class StorageSyncService
{
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _scopeFactory;

    public StorageSyncService(HttpClient httpClient, IServiceScopeFactory scopeFactory)
    {
        _httpClient = httpClient;
        _scopeFactory = scopeFactory;
    }

    public async Task<int?> FindStorageAsync(string orderId, string kaspiCode, string token)
{
    var orderUrl = $"https://kaspi.kz/shop/api/v2/orders?filter[orders][code]={kaspiCode}";
    _httpClient.DefaultRequestHeaders.Clear();
    _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", token);
    _httpClient.DefaultRequestHeaders.Add("ContentType", "application/vnd.api+json");
    
    try
    {
        var entriesResponse = await _httpClient.GetAsync(orderUrl);
        entriesResponse.EnsureSuccessStatusCode();
        var entriesJson = await entriesResponse.Content.ReadAsStringAsync();
        var entriesData = JsonSerializer.Deserialize<JsonElement>(entriesJson);

        if (!entriesData.TryGetProperty("data", out var dataArray) || dataArray.ValueKind != JsonValueKind.Array || dataArray.GetArrayLength() == 0)
        {
            Console.WriteLine($"[ERROR] No order data found for order {kaspiCode}");
            return null;
        }

        var orderAttributes = dataArray[0].GetProperty("attributes");
        var originAddress = orderAttributes.GetProperty("originAddress");
        var city = originAddress.GetProperty("city").GetProperty("name").GetString();
        var streetName = originAddress.GetProperty("address").GetProperty("streetName").GetString();
        var streetNumber = originAddress.GetProperty("address").GetProperty("streetNumber").GetString();
        var originAddressId = originAddress.GetProperty("id").GetString();

        if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(streetName) || string.IsNullOrEmpty(originAddressId))
        {
            Console.WriteLine($"[ERROR] Invalid address data for order {kaspiCode}");
            return null;
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storages = await context.Storages
            .Where(w => w.city == city)
            .ToListAsync();

        var matchedWarehouse = storages
            .Select(w => new
            {
                Warehouse = w,
                Score = CalculateMatchScore(w, streetName, streetNumber)
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault(x => x.Score > 80);

        if (matchedWarehouse == null)
        {
            Console.WriteLine($"[WARNING] No warehouse found for order {kaspiCode}, address {originAddressId}");
            return null;
        }

        return matchedWarehouse.Warehouse.id;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Error processing order {kaspiCode}: {ex.Message}");
        return null;
    }
}
    private int CalculateMatchScore(Storage storage, string streetName, string streetNumber)
    {
        int score = 0;

        if (string.IsNullOrWhiteSpace(storage.address))
            return 0;

        var address = storage.address.Trim().ToLower();
        var normalizedStreet = streetName.Trim().ToLower();
        var normalizedNumber = streetNumber.Trim().ToLower();

        if (address.Contains(normalizedStreet))
            score += 60;

        if (address.Contains(normalizedNumber))
            score += 40;

        return score;
    }
}