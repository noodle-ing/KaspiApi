using Jetqor_kaspi_api.Enum;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace Jetqor_kaspi_api.Services;

public class KaspiOrderService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OrderSyncService _orderSyncService;

    public KaspiOrderService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory, OrderSyncService orderSyncService)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _orderSyncService = orderSyncService;
    }

    public async Task CheckAndSaveOrdersOnceAsync(string token)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            var kazakhstanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty");

            var end = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, kazakhstanTimeZone);
            var start = end.AddDays(-1);

            long startTimestamp = new DateTimeOffset(start, kazakhstanTimeZone.GetUtcOffset(start)).ToUnixTimeMilliseconds();
            long endTimestamp = new DateTimeOffset(end, kazakhstanTimeZone.GetUtcOffset(end)).ToUnixTimeMilliseconds();
            
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("X-Auth-Token", token);

            
            var url = $"https://kaspi.kz/shop/api/v2/orders" +
                      $"?page[number]=0&page[size]=100" +
                      $"&filter[orders][creationDate][$ge]={startTimestamp}" +
                      $"&filter[orders][creationDate][$le]={endTimestamp}" +
                      $"&filter[orders][state]=ARCHIVE" +
                      $"&include[orders]=user";

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            Console.WriteLine(url);

            var json = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(json);

            var included = obj["included"] != null
                ? obj["included"]
                    .ToDictionary(
                        x => (string)x["id"],
                        x => (dynamic)new
                        {
                            name = ((string)x["attributes"]?["firstName"] ?? "") + " " + ((string)x["attributes"]?["lastName"] ?? ""),
                            phone = (string)x["attributes"]?["cellPhone"] ?? ""
                        })
                : new Dictionary<string, dynamic>();

            int newOrders = 0, skippedOrders = 0;

            foreach (var order in obj["data"])
            {
                var attributes = order["attributes"];
                string code = attributes["code"].ToObject<string>();                
                string id = order["id"].ToObject<string>();
                var kaspiCode = (string)order["attributes"]?["code"];
                if (db.Orders.Any(o => o.kaspi_code == kaspiCode))
                {
                    skippedOrders++;
                    continue;
                }

                int newId = db.Orders.OrderByDescending(o => o.Id).Select(o => o.Id).FirstOrDefault() + 1;
                string statusStr = ((string)order["attributes"]?["status"])?.ToUpperInvariant() ?? "";

                Status status = MapOrderStatus(statusStr);
                KaspiStatus kaspiStatus = MapKaspiStatus(statusStr);

                string customerId = (string)order["relationships"]?["user"]?["data"]?["id"];
                
                var user = db.Users.FirstOrDefault(u => u.kaspi_key == token);
                string customerName = user.name;
                
                string customerPhone = included.ContainsKey(customerId) ? included[customerId].phone : "";

                var newOrder = new Order
                {
                    Id = newId,
                    kaspi_code = kaspiCode,
                    kaspi_status = kaspiStatus,
                    status = status,
                    marketplace_created_at = DateTimeOffset.FromUnixTimeMilliseconds((long)order["attributes"]["creationDate"]).UtcDateTime,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow,
                    total_price = (int?)order["attributes"]?["totalPrice"] ?? 0,
                    delivery_cost = (int?)order["attributes"]?["deliveryCost"] ?? 0,
                    express = (int?)order["attributes"]?["express"] ?? 0,
                    customer_name = customerName,
                    customer_phone = customerPhone
                };

                db.Orders.Add(newOrder);
                await db.SaveChangesAsync();
                
                // Синхронизируем детали заказа ПОСЛЕ его создания в базе
                try
                {
                    await _orderSyncService.SyncOrderAsync(code, token, id);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING] Failed to sync order details for {code}: {ex.Message}");
                }
                
                newOrders++;
            }

            Console.WriteLine($"[SUMMARY] Done. Added {newOrders} orders, skipped {skippedOrders}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
            throw;
        }
        
    }

    private KaspiStatus MapKaspiStatus(string value)
    {
        return value switch
        {
            "APPROVED_BY_BANK" => KaspiStatus.APPROVED_BY_BANK,
            "ACCEPTED_BY_MERCHANT" => KaspiStatus.ACCEPTED_BY_MERCHANT,
            "COMPLETED" => KaspiStatus.COMPLETED,
            "CANCELLED" => KaspiStatus.CANCELLED,
            "CANCELLING" => KaspiStatus.CANCELLING,
            "KASPI_DELIVERY_RETURN_REQUESTED" => KaspiStatus.KASPI_DELIVERY_RETURN_REQUESTED,
            "RETURNED" => KaspiStatus.RETURNED,
            _ => throw new Exception($"Unknown KaspiStatus: {value}")
        };
    }

    private Status MapOrderStatus(string value)
    {
        return value.ToLower() switch
        {
            "APPROVED_BY_BANK" => Status.assembly,
            "ACCEPTED_BY_MERCHANT" => Status.assembly,
            "COMPLETED" => Status.completed,
            "CANCELLED" => Status.cancelled,
            "CANCELLING" => Status.cancelled,
            "KASPI_DELIVERY_RETURN_REQUESTED" => Status.cancelled,
            "RETURNED" => Status.cancelled,
            _ => Status.assembly
        };
    }
}
