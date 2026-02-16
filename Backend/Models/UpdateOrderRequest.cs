using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public record UpdateOrderRequest
{
    [Required, MinLength(1, ErrorMessage = "At least one line item is required.")]
    public List<CreateOrderLineItemRequest> LineItems { get; init; } = [];
}
