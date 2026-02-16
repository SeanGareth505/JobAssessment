using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public record UpdateCustomerRequest
{
    [Required, MinLength(1), MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(256)]
    [EmailAddress]
    public string? Email { get; init; }

    [Required, MinLength(2), MaxLength(2)]
    public string CountryCode { get; init; } = string.Empty;
}
