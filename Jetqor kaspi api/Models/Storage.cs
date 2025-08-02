using System.ComponentModel.DataAnnotations.Schema;
namespace Jetqor_kaspi_api.Models;
[Table("Storage")]
public class Storage
{
    public int id { get; set; }
    public DateTime created_at { get; set; }
    public DateTime updatedAt{ get; set; }
    public string name{ get; set; }
    public string address{ get; set; }
    public string city{ get; set; }
}