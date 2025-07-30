using System.ComponentModel.DataAnnotations.Schema;

namespace Jetqor_kaspi_api.Models;
[Table("OrderProduct")]
public class OrderProduct
{
    [Column("id")]
    public int id { get; set; }
    [Column("orderId")]
    public int orderId { get; set; }
    [Column("productId")]
    public int productId { get; set; }
    [Column("count")]
    public int count {get; set;}
    
    public Order Order { get; set; }
    public Product Product { get; set; }
}