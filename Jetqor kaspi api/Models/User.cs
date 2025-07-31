using System.ComponentModel.DataAnnotations.Schema;

namespace Jetqor_kaspi_api.Models;
[Table("User")]

public class User
{
    public int id { get; set; }
    public DateTime created_at { get; set; }
    public string email { get; set; }
    public string name { get; set; }
    public string phone { get; set; }
    public string role { get; set; }
    public string password { get; set; }
    public bool blocked { get; set; }
    public string? kaspi_key { get; set; }
    public string? ozon_key { get; set; }
    public string? wildberries_key { get; set; }
    public int? storage_id { get; set; }
}