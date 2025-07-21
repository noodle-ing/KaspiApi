using Jetqor_kaspi_api.Enum;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Jetqor_kaspi_api.Services;

public class KaspiOrderChecker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;

    public KaspiOrderChecker(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckAndSaveOrdersOnceAsync();
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }

    public async Task CheckAndSaveOrdersOnceAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Auth-Token", "fq4r4x1n7ngMvU/SB8y6SAdPjElXZ8K8g0ORtk+3FXI=");

        var url = "https://kaspi.kz/shop/api/v2/orders?page[number]=0&page[size]=20&filter[orders][state]=NEW";

        try
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

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

            foreach (var order in obj["data"])
            {
                var kaspiCode = (string)order["attributes"]?["code"];
                bool exists = db.Orders.Any(o => o.kaspi_code == kaspiCode);
                if (exists) continue;

                int newId = (db.Orders.OrderByDescending(o => o.Id).Select(o => o.Id).FirstOrDefault()) + 1;

                string statusStr = ((string)order["attributes"]?["status"])?.ToUpperInvariant() ?? "";
                string stateStr = ((string)order["attributes"]?["state"])?.ToUpperInvariant() ?? "";

                Status status = MapOrderStatus(statusStr);
                KaspiStatus kaspiStatus = MapKaspiStatus(stateStr);

                string customerId = (string)order["relationships"]?["user"]?["data"]?["id"];
                string customerName = included.ContainsKey(customerId) ? included[customerId].name : "";
                string customerPhone = included.ContainsKey(customerId) ? included[customerId].phone : "";

                db.Orders.Add(new Order
                {
                    Id = newId,
                    kaspi_code = kaspiCode,
                    kaspi_status = kaspiStatus,
                    status = status,
                    marketplace_created_at = DateTimeOffset.FromUnixTimeMilliseconds((long)order["attributes"]["creationDate"]).UtcDateTime,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow,
                    total_price = (double?)order["attributes"]?["totalPrice"] ?? 0,
                    delivery_cost = (double?)order["attributes"]?["deliveryCost"] ?? 0,
                    express = (int?)order["attributes"]?["express"] ?? 0,
                    customer_name = customerName,
                    customer_phone = customerPhone
                });

                await db.SaveChangesAsync();
                Console.WriteLine($"[+] Новый заказ добавлен: {kaspiCode} (ID {newId})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Ошибка] {ex.Message}");
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
            "PICKUP" => KaspiStatus.ACCEPTED_BY_MERCHANT, 
            _ => throw new Exception($"Неизвестный KaspiStatus: {value}")
        };
    }

    private Status MapOrderStatus(string value)
    {
        return value switch
        {
            "CANCELLED" => Status.cancelled,
            "COMPLETED" => Status.completed,
            "CANCELLING" => Status.assembly,
            "KASPI_DELIVERY_RETURN_REQUESTED" => Status.indelivery,
            "APPROVED_BY_BANK" => Status.waiting,
            "ACCEPTED_BY_MERCHANT" => Status.packed,
            "PICKUP" => Status.indelivery, 
            _ => throw new Exception($"Неизвестный OrderStatus: {value}")
        };
    }
}
