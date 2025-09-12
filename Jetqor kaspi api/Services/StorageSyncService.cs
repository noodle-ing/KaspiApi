using System.Text.Json;
using Jetqor_kaspi_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Jetqor_kaspi_api.Services;

public class StorageSyncService
{
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _scopeFactory;

    private static readonly string[] StopWords = new[]
    {
        "улица", "ул", "проспект", "пр", "город", "г", "астана", "алматы",
        "казахстан", "рк", "дом", "д", "микрорайон", "мкр", "кз"
    };

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
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.api+json");
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

            var inputTokens = NormalizeAddress(streetName).Concat(NormalizeAddress(streetNumber)).ToList();

            var manualRules = new List<(List<string> tokens, int id)>
            {
                (new List<string>{ "хаби", "халиуллина", "66", "11", "1" }, 18),
                (new List<string>{ "чаплина", "71" }, 17),
                (new List<string>{ "кенсаз", "3", "1" }, 16),
                (new List<string>{ "туйменбаева", "754" }, 15)
            };


            foreach (var rule in manualRules)
            {
                if (rule.tokens.All(t => inputTokens.Contains(t)))
                    return rule.id;
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var storages = await context.Storages.Where(w => w.city == city).ToListAsync();

            foreach (var storage in storages)
            {
                var addressTokens = NormalizeAddress(storage.address);
                bool allMatch = inputTokens.All(token => addressTokens.Contains(token));

                if (allMatch)
                    return storage.id;
            }

            Console.WriteLine($"[WARNING] No matching warehouse found for order {kaspiCode}, address {originAddressId}");
            return null; // теперь null, а не 0
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Error processing order {kaspiCode}: {ex.Message}");
            return null;
        }
    }

    private List<string> NormalizeAddress(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new List<string>();

        return input
            .ToLower()
            .Replace("\\", " ")
            .Replace("/", " ")
            .Replace("-", " ")
            .Replace(",", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !StopWords.Contains(token))
            .ToList();
    }
}
