namespace Backend.Models;

public record UpdateOrderStatusRequest
{
    public OrderStatusDto Status { get; init; }
}
