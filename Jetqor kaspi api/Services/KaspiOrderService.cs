using System.Text.Json;
using Jetqor_kaspi_api.Enum;
using Jetqor_kaspi_api.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace Jetqor_kaspi_api.Services;

public class KaspiOrderService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OrderSyncService _orderSyncService;
    private readonly StorageSyncService _storageSyncService;
    private readonly AcceptanceStatusGiverService _acceptanceStatusGiverService;
    private readonly ReturnProductService _returnProductService;

    public KaspiOrderService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory, OrderSyncService orderSyncService, 
        StorageSyncService storageSyncService, AcceptanceStatusGiverService acceptanceStatusGiverService,
        ReturnProductService returnProductService)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _orderSyncService = orderSyncService;
        _storageSyncService = storageSyncService;
        _acceptanceStatusGiverService = acceptanceStatusGiverService;
        _returnProductService = returnProductService;
    }

    public async Task CheckAndSaveOrdersOnceAsync()
    {
       // await UpdateOldOrdersStatusesAsync();
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var usersWithTokens = await db.Users
            .Where(u => u.kaspi_key != null)
            .ToListAsync();

        int totalNewOrders = 0, totalSkippedOrders = 0;

        var kazakhstanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent");

        var end = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, kazakhstanTimeZone);
        var start = end.AddDays(-1);

        long startTimestamp = new DateTimeOffset(start, kazakhstanTimeZone.GetUtcOffset(start)).ToUnixTimeMilliseconds();
        long endTimestamp = new DateTimeOffset(end, kazakhstanTimeZone.GetUtcOffset(end)).ToUnixTimeMilliseconds();

        foreach (var user in usersWithTokens)
        {
            var token = user.kaspi_key;

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("X-Auth-Token", token);

                var url = $"https://kaspi.kz/shop/api/v2/orders" +
                          $"?page[number]=0&page[size]=100" +
                          $"&filter[orders][creationDate][$ge]={startTimestamp}" +
                          $"&filter[orders][creationDate][$le]={endTimestamp}" +
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
                    string statusStr = ((string)order["attributes"]?["status"])?.ToUpperInvariant() ?? "";
                    // await _acceptanceStatusGiverService.UpdateOrderStatusAsync(id, token);
                    if (db.Orders.Any(o => o.kaspi_code == kaspiCode))
                    {
                        skippedOrders++;
                        continue;
                    }

                    int newId = db.Orders.OrderByDescending(o => o.Id).Select(o => o.Id).FirstOrDefault() + 1;

                    Status status = MapOrderStatus(statusStr);
                    KaspiStatus kaspiStatus = MapKaspiStatus(statusStr);

                    string customerId = (string)order["relationships"]?["user"]?["data"]?["id"];
                    
                    string customerName = db.Users.Where(u => u.kaspi_key == token)
                        .Select( u => u.name).FirstOrDefault() ?? "";
                    
                    string customerPhone = included.ContainsKey(customerId) ? included[customerId].phone : "";
                    var storageId = await _storageSyncService.FindStorageAsync(code, kaspiCode, token);
                    
                   
                    
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
                        customer_phone = customerPhone,
                        storage_id = storageId
                    };

                    db.Orders.Add(newOrder);
                    await db.SaveChangesAsync();
                    
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

                totalNewOrders += newOrders;
                totalSkippedOrders += skippedOrders;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed for token of user {user.id}: {ex.Message}");
            }
        }

        Console.WriteLine($"[SUMMARY] Done. Added {totalNewOrders} orders, skipped {totalSkippedOrders}.");
    }

    public async Task<string> GetConsignment(string orderId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = await db.Orders.FirstOrDefaultAsync(o => o.kaspi_code == orderId);
        if (order == null)
            return "Order not found";

        var customerName = order.customer_name;
        if (string.IsNullOrEmpty(customerName))
            return "Customer name is missing";

        var user = await db.Users.FirstOrDefaultAsync(u => u.name == customerName);
        if (user == null || string.IsNullOrEmpty(user.kaspi_key))
            return "User or token not found";

        var token = user.kaspi_key;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("X-Auth-Token", token);
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.api+json");

        var url = $"https://kaspi.kz/shop/api/v2/orders?filter[orders][code]={orderId}";
        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return "Error occurred";

        var json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        try
        {
            var waybill = doc.RootElement
                .GetProperty("data")[0]
                .GetProperty("attributes")
                .GetProperty("kaspiDelivery")
                .GetProperty("waybill")
                .GetString();
            return waybill ?? "Waybill not found, firstly move order from packaging to delivery";
        }
        catch(Exception ex)
        {
            return $"Error occurred: {ex.Message}";
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
        return value switch
        {
            "APPROVED_BY_BANK" => Status.packaging,
            "ACCEPTED_BY_MERCHANT" => Status.packaging,
            "COMPLETED" => Status.completed,
            "CANCELLED" => Status.cancelled,
            "CANCELLING" => Status.cancelled,
            "KASPI_DELIVERY_RETURN_REQUESTED" => Status.return_request,
            "RETURNED" => Status.Return,
            _ => Status.assembly
        };
    }


private async Task UpdateOldOrdersStatusesAsync()
{
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var usersWithTokens = await db.Users
        .Where(u => u.kaspi_key != null)
        .ToListAsync();

    var kazakhstanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent");
    var end = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, kazakhstanTimeZone);

    int[] intervals = { 3, 7, 14 };

    int updatedOrders = 0, skippedOrders = 0;

    foreach (var user in usersWithTokens)
    {
        var token = user.kaspi_key;

        foreach (var days in intervals)
        {
            var start = end.AddDays(-days);

            long startTimestamp = new DateTimeOffset(start, kazakhstanTimeZone.GetUtcOffset(start)).ToUnixTimeMilliseconds();
            long endTimestamp = new DateTimeOffset(end, kazakhstanTimeZone.GetUtcOffset(end)).ToUnixTimeMilliseconds();

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("X-Auth-Token", token);

                var url = $"https://kaspi.kz/shop/api/v2/orders" +
                          $"?page[number]=0&page[size]=100" +
                          $"&filter[orders][creationDate][$ge]={startTimestamp}" +
                          $"&filter[orders][creationDate][$le]={endTimestamp}" +
                          $"&include[orders]=user";

                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);

                foreach (var order in obj["data"])
                {
                    var kaspiCode = (string)order["attributes"]?["code"];
                    var statusStr = ((string)order["attributes"]?["status"])?.ToUpperInvariant() ?? "";
                    string id = order["id"].ToObject<string>();

                    var dbOrder = await db.Orders.FirstOrDefaultAsync(o => o.kaspi_code == kaspiCode);
                    if (dbOrder == null) continue;

                    var newKaspiStatus = MapKaspiStatus(statusStr);
                    var newStatus = MapOrderStatus(statusStr);

                    if (statusStr == "CANCELLED" || statusStr == "CANCELLING" || statusStr == "RETURNED")
                    {
                        await _returnProductService.ReturnProduct(id, token, user.id);
                    }

                    if (dbOrder.kaspi_status != newKaspiStatus || dbOrder.status != newStatus)
                    {
                        dbOrder.kaspi_status = newKaspiStatus;
                        dbOrder.status = newStatus;
                        dbOrder.updated_at = DateTime.UtcNow;

                        await db.SaveChangesAsync();
                        updatedOrders++;
                    }
                    else
                    {
                        skippedOrders++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed for token of user {user.id}, interval {days} days: {ex.Message}");
            }
        }
    }

    Console.WriteLine($"[SUMMARY] Status update done. Updated {updatedOrders}, skipped {skippedOrders}.");
}

}