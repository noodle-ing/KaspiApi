using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jetqor_kaspi_api.Models;
[Table("Product")]

public class Product
{
    [Key]
    public int id { get; set; }
    public string name { get; set; }
    public string article { get; set; }
    public string photo_url { get; set; }
    public string description { get; set; }
    public int sells_count_month { get; set; }
    public DateTime? last_sell_dt { get; set; }
    public DateTime? created_at { get; set; }
    public DateTime? updated_at { get; set; }
    public int userId { get; set; }
    public int  price { get; set; }
    public short integration_kaspi { get; set; }
    public short integration_wildberries { get; set; }
    public short integration_ozon { get; set; }
    public short best_seller { get; set; }
    public int height { get; set; }
    public int length { get; set; }
    public int volume { get; set; }
    public int weight { get; set; }
    public int width { get; set; }
    
    public ICollection<OrderProduct> OrderProducts { get; set; }
}