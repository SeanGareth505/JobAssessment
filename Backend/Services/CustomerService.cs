using System.Text.Json;
using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class CustomerService(
    AppDbContext db,
    IRabbitMqService rabbitMq,
    ISadcValidationService sadcValidation,
    ILogger<CustomerService> logger) : ICustomerService
{
    private const string CustomerCreatedQueue = "customer-created";

    public async Task<PagedResult<CustomerDto>> GetPageAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Name.Contains(term) || (c.Email != null && c.Email.Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var customerIds = items.Select(c => c.Id).ToList();
        var orderCounts = await db.Orders
            .AsNoTracking()
            .Where(o => customerIds.Contains(o.CustomerId))
            .GroupBy(o => o.CustomerId)
            .Select(g => new { CustomerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Count, ct);

        var dtos = items.Select(c => MapToDto(c) with { OrderCount = orderCounts.GetValueOrDefault(c.Id, 0) }).ToList();

        return new PagedResult<CustomerDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return customer == null ? null : MapToDto(customer);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        if (!sadcValidation.IsValidCountryCode(request.CountryCode))
        {
            logger.LogWarning("Invalid SADC country code: {CountryCode}", request.CountryCode);
            throw new InvalidOperationException($"Invalid or unsupported SADC country code: {request.CountryCode}.");
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = (request.Email ?? string.Empty).Trim(),
            CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        try
        {
            var payloadObj = new
            {
                customer.Id,
                customer.Name,
                customer.Email,
                customer.CountryCode,
                OccurredAtUtc = customer.CreatedAt
            };
            var payload = JsonSerializer.Serialize(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            rabbitMq.Publish(CustomerCreatedQueue, payload);
            logger.LogInformation("CustomerCreated published for {CustomerId} to {Queue}.", customer.Id, CustomerCreatedQueue);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish CustomerCreated for {CustomerId}.", customer.Id);
        }

        return MapToDto(customer);
    }

    public async Task<CustomerDto?> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default)
    {
        if (!sadcValidation.IsValidCountryCode(request.CountryCode))
        {
            logger.LogWarning("Invalid SADC country code on update: {CountryCode}", request.CountryCode);
            throw new InvalidOperationException($"Invalid or unsupported SADC country code: {request.CountryCode}.");
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer == null)
            return null;

        customer.Name = request.Name.Trim();
        customer.Email = (request.Email ?? string.Empty).Trim();
        customer.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Updated customer {CustomerId}", customer.Id);
        return MapToDto(customer);
    }

    private static CustomerDto MapToDto(Customer c) =>
        new(c.Id, c.Name, c.Email, c.CountryCode, c.CreatedAt);
}
