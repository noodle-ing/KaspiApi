using Jetqor_kaspi_api;
using Newtonsoft.Json.Linq;

public class KaspiOrderService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<KaspiOrderService> _logger;

    public KaspiOrderService(HttpClient httpClient, AppDbContext dbContext, ILogger<KaspiOrderService> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task FetchAndSaveOrdersAsync()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var tenMinutesAgo = now - TimeSpan.FromMinutes(1).TotalMilliseconds;

        string url = $"https://kaspi.kz/shop/api/v2/orders?page[number]=0&page[size]=100" +
                     $"&filter[orders][creationDate][$ge]={tenMinutesAgo}" +
                     $"&filter[orders][creationDate][$le]={now}" +
                     $"&filter[orders][state]=ARCHIVE";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Auth-Token", "fq4r4x1n7ngMvU/SB8y6SAdPjElXZ8K8g0ORtk+3FXI=");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);

        foreach (var item in json["data"])
        {
            var attributes = item["attributes"];
            var customer = attributes["customer"];
            var address = attributes["deliveryAddress"];

            var order = new Order
            {
                KaspiOrderId = (string)item["id"],
                Code = (string)attributes["code"],
                CreationDate = (long)attributes["creationDate"],
                TotalPrice = (double)attributes["totalPrice"],
                Status = (string)attributes["status"],
                State = (string)attributes["state"],
                CustomerName = (string)customer["name"],
                CustomerPhone = (string)customer["cellPhone"],
                Address = (string)address["formattedAddress"]
            };

            if (!_dbContext.Orders.Any(o => o.KaspiOrderId == order.KaspiOrderId))
            {
                _dbContext.Orders.Add(order);
            }
        }

        await _dbContext.SaveChangesAsync();
    }
}
