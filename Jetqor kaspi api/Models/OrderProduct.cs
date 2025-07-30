namespace Jetqor_kaspi_api.Models;

public class OrderProduct
{
    public int id { get; set; }
    public int orderId { get; set; }
    public int productId { get; set; }
    public int count {get; set;}
}