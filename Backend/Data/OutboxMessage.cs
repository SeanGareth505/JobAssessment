namespace Backend.Data;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string AggregateType { get; set; } = "Order";
    public Guid AggregateId { get; set; }
    public string Type { get; set; } = "OrderCreated";
    public string Payload { get; set; } = "{}";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
    public int Version { get; set; } = 1;
}
