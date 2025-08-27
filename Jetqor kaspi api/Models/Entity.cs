using System.ComponentModel.DataAnnotations.Schema;

namespace Jetqor_kaspi_api.Models;
[Table("Entity")]

public class Entity
{
    public int id {get;set;}
    public DateTime created_at {get;set;}
    public DateTime updated_at {get;set;}
    public int count { get; set; }
    public int productId { get; set; }
    public int cellId { get; set; }
    public int ownerId { get; set; }
}