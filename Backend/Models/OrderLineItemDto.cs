namespace Backend.Models;

public record OrderLineItemDto(
    Guid Id,
    Guid OrderId,
    Guid? ProductId,
    string ProductSku,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
