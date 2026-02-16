namespace Backend.Models;

public record CustomerDto(Guid Id, string Name, string Email, string CountryCode, DateTime CreatedAt)
{
    public int OrderCount { get; init; }
}
