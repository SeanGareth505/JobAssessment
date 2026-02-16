using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public record CreateOrderRequest
{
    public Guid CustomerId { get; init; }

    [Required, MinLength(3), MaxLength(3)]
    public string CurrencyCode { get; init; } = "ZAR";

    [Required, MinLength(1, ErrorMessage = "At least one line item is required.")]
    public List<CreateOrderLineItemRequest> LineItems { get; init; } = [];
}

public record CreateOrderLineItemRequest
{
    [Required(ErrorMessage = "Each order line must reference a product from the catalog.")]
    public Guid ProductId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; init; } = 1;

    [Range(0, double.MaxValue, ErrorMessage = "Unit price must be zero or greater.")]
    public decimal UnitPrice { get; init; }
}
