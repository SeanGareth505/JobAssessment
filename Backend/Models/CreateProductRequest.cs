using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public record CreateProductRequest
{
    [Required, MinLength(1), MaxLength(200)]
    public string Sku { get; init; } = string.Empty;

    [Required, MinLength(1), MaxLength(200)]
    public string Name { get; init; } = string.Empty;
}
