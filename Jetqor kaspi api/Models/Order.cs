using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Jetqor_kaspi_api.Enum;
[Table("Order")]
public class Order
{
    [Key]
    public int Id { get; set; }
    public string kaspi_code { get; set; }
    public string? wildberries_code { get; set; }
    public string? ozon_code { get; set; }
    public DateTime created_at { get; set; }
    public DateTime updated_at { get; set; }
    public DateTime marketplace_created_at { get; set; }
    public double total_price { get; set; }
    public double delivery_cost { get; set; }
    
    public KaspiStatus kaspi_status { get; set; }
    public Status status { get; set; }
    
    public string customer_phone { get; set; }
    public string customer_name { get; set; }
    public int express { get; set; }
}