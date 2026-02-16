namespace Backend.Data;

public class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CurrencyCode { get; set; } = "ZAR";
    public decimal TotalAmount { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Customer Customer { get; set; } = null!;
    public ICollection<OrderLineItem> LineItems { get; set; } = new List<OrderLineItem>();
}
