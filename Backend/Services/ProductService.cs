using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class ProductService(AppDbContext db, ILogger<ProductService> logger) : IProductService
{
    public async Task<PagedResult<ProductDto>> GetPageAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.Sku.Contains(term) || (p.Name != null && p.Name.Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Sku)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ProductDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            page,
            pageSize);
    }

    public async Task<ProductDto?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;
        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Sku == sku.Trim(), ct);
        return product == null ? null : MapToDto(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var sku = request.Sku.Trim();
        var exists = await db.Products.AnyAsync(p => p.Sku == sku, ct);
        if (exists)
            throw new InvalidOperationException($"A product with SKU '{sku}' already exists.");

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = sku,
            Name = request.Name.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created product {ProductId} with SKU {Sku}.", product.Id, product.Sku);
        return MapToDto(product);
    }

    private static ProductDto MapToDto(Product p) =>
        new ProductDto(p.Id, p.Sku, p.Name ?? string.Empty, p.CreatedAt);
}
