namespace Backend.Models;

public record ProductDto(Guid Id, string Sku, string Name, DateTime CreatedAt);
