using Backend.Models;

namespace Backend.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetPageAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<ProductDto?> GetBySkuAsync(string sku, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
}
