namespace Backend.Models;

public record OrderDto(
    Guid Id,
    Guid CustomerId,
    OrderStatusDto Status,
    DateTime CreatedAt,
    string CurrencyCode,
    decimal TotalAmount,
    string? CustomerName,
    IReadOnlyList<OrderLineItemDto> LineItems,
    string? Etag);

public enum OrderStatusDto
{
    Pending = 0,
    Paid = 1,
    Fulfilled = 2,
    Cancelled = 3
}
