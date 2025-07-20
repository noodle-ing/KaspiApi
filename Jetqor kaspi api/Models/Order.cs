public class Order
{
    public int Id { get; set; }
    public string KaspiOrderId { get; set; }
    public string Code { get; set; }
    public long CreationDate { get; set; }
    public double TotalPrice { get; set; }
    public string Status { get; set; }
    public string State { get; set; }
    public string CustomerName { get; set; }
    public string CustomerPhone { get; set; }
    public string Address { get; set; }
}