using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Jetqor_kaspi_api.Services
{
    public class AcceptanceStatusGiverService
    {
        private readonly HttpClient _httpClient;
        private const string KaspiApiUrl = "https://kaspi.kz/shop/api/v2/orders";

        public AcceptanceStatusGiverService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, string token)
        {
            if (orderId <= 0)
            {
                Console.WriteLine("Error: Order ID must be greater than zero.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("Error: Auth token cannot be null or empty.");
                return false;
            }

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", token);

                var requestBody = new
                {
                    data = new
                    {
                        type = "orders",
                        id = orderId.ToString(), 
                        attributes = new
                        {
                            status = "ASSEMBLE",
                            numberOfSpace = "1"
                        }
                    }
                };

                var jsonRequest = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/vnd.api+json");

                Console.WriteLine($"Sending POST request to {KaspiApiUrl} for order ID: {orderId}");
                var response = await _httpClient.PostAsync(KaspiApiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

                    if (responseObject?.data != null)
                    {
                        Console.WriteLine($"Success: Order {orderId} updated successfully. Response: {jsonResponse}");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"Error: Empty or invalid response received for order ID: {orderId}");
                        return false;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: Failed to update order {orderId}. Status: {response.StatusCode}, Details: {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Exception occurred while updating order {orderId}: {ex.Message}");
                return false;
            }
        }
    }
}
