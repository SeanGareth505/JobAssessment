using System.Text.Json;
using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Services;

public class OrderService(
    AppDbContext db,
    ISadcValidationService sadcValidation,
    IMemoryCache cache,
    ILogger<OrderService> logger) : IOrderService
{
    private static readonly TimeSpan IdempotencyCacheDuration = TimeSpan.FromHours(24);

    public async Task<PagedResult<OrderDto>> GetPageAsync(Guid? customerId, OrderStatusDto? status, int page, int pageSize, string? sort, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.LineItems)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(o => o.CustomerId == customerId.Value);
        if (status.HasValue)
            query = query.Where(o => o.Status == (OrderStatus)status.Value);

        var totalCount = await query.CountAsync(ct);

        query = ApplySort(query, sort);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OrderDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            page,
            pageSize);
    }

    public async Task<OrderDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        return order == null ? null : MapToDto(order);
    }

    public async Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct);
        if (customer == null)
            throw new InvalidOperationException("Customer not found.");

        if (!sadcValidation.IsValidCurrencyForCountry(customer.CountryCode, request.CurrencyCode))
            throw new InvalidOperationException($"Currency {request.CurrencyCode} is not valid for customer's country {customer.CountryCode}.");

        if (request.LineItems == null || request.LineItems.Count == 0)
            throw new InvalidOperationException("At least one product line is required.");

        foreach (var line in request.LineItems)
        {
            if (line.Quantity < 1)
                throw new InvalidOperationException("Quantity must be at least 1 for each product line.");
            if (line.UnitPrice < 0)
                throw new InvalidOperationException("Unit price must be zero or greater for each product line.");
        }

        decimal totalAmount = 0;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            TotalAmount = 0
        };

        foreach (var line in request.LineItems)
        {
            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == line.ProductId, ct);
            if (product == null)
                throw new InvalidOperationException($"The selected product was not found. Each order line must reference a product from the catalog.");

            var lineTotal = line.Quantity * line.UnitPrice;
            totalAmount += lineTotal;
            order.LineItems.Add(new OrderLineItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = product.Id,
                ProductSku = product.Sku,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice
            });
        }
        order.TotalAmount = totalAmount;

        var payload = JsonSerializer.Serialize(new
        {
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.CurrencyCode,
            OccurredAtUtc = order.CreatedAt,
            LineItems = order.LineItems.Select(li => new { li.Id, li.OrderId, li.ProductSku, li.Quantity, li.UnitPrice }).ToList()
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        db.Orders.Add(order);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateType = "Order",
            AggregateId = order.Id,
            Type = "OrderCreated",
            Payload = payload,
            OccurredAtUtc = order.CreatedAt,
            ProcessedAtUtc = null,
            Version = 1
        });
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Order {OrderId} and OrderCreated outbox message saved (will be published by background process).", order.Id);

        order.Customer = customer;
        return MapToDto(order);
    }

    public async Task<OrderDto?> UpdateAsync(Guid orderId, UpdateOrderRequest request, CancellationToken ct = default)
    {
        var order = await db.Orders
            .Include(o => o.Customer)
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order == null)
            return null;

        if (order.Status != OrderStatus.Pending)
            throw new InvalidOperationException("Only Pending orders can be edited.");

        if (request.LineItems == null || request.LineItems.Count == 0)
            throw new InvalidOperationException("At least one product line is required.");

        foreach (var line in request.LineItems)
        {
            if (line.Quantity < 1)
                throw new InvalidOperationException("Quantity must be at least 1 for each product line.");
            if (line.UnitPrice < 0)
                throw new InvalidOperationException("Unit price must be zero or greater for each product line.");
        }

        if (!sadcValidation.IsValidCurrencyForCountry(order.Customer.CountryCode, order.CurrencyCode))
            throw new InvalidOperationException($"Currency {order.CurrencyCode} is not valid for customer's country {order.Customer.CountryCode}.");

        order.LineItems.Clear();
        decimal totalAmount = 0;
        foreach (var line in request.LineItems)
        {
            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == line.ProductId, ct);
            if (product == null)
                throw new InvalidOperationException($"The selected product was not found. Each order line must reference a product from the catalog.");

            var lineTotal = line.Quantity * line.UnitPrice;
            totalAmount += lineTotal;
            order.LineItems.Add(new OrderLineItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = product.Id,
                ProductSku = product.Sku,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice
            });
        }
        order.TotalAmount = totalAmount;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("Order was updated by another process (concurrency conflict).");
        }

        logger.LogInformation("Updated order {OrderId} (line items and total).", order.Id);
        return MapToDto(order);
    }

    public async Task<OrderDto?> UpdateStatusAsync(Guid orderId, OrderStatusDto status, string? idempotencyKey, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cacheKey = $"idempotency:status:{orderId}:{idempotencyKey.Trim()}";
            if (cache.TryGetValue(cacheKey, out OrderDto? cached))
                return cached;
        }

        var order = await db.Orders
            .Include(o => o.Customer)
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order == null)
            return null;

        var newStatus = (OrderStatus)status;
        if (!IsValidTransition(order.Status, newStatus))
            throw new InvalidOperationException($"Invalid status transition from {order.Status} to {newStatus}.");

        order.Status = newStatus;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("Order was updated by another process (concurrency conflict).");
        }

        var dto = MapToDto(order);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cacheKey = $"idempotency:status:{orderId}:{idempotencyKey.Trim()}";
            cache.Set(cacheKey, dto, IdempotencyCacheDuration);
        }
        return dto;
    }

    private static bool IsValidTransition(OrderStatus from, OrderStatus to)
    {
        return (from, to) switch
        {
            (OrderStatus.Pending, OrderStatus.Paid) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.Paid, OrderStatus.Fulfilled) => true,
            (OrderStatus.Paid, OrderStatus.Cancelled) => true,
            _ => false
        };
    }

    private static IQueryable<Order> ApplySort(IQueryable<Order> query, string? sort)
    {
        var by = (sort ?? "createdAt").Trim().ToLowerInvariant();
        var desc = by.StartsWith("-");
        var field = desc ? by.TrimStart('-') : by;

        return field switch
        {
            "createdat" or "created_at" => desc ? query.OrderByDescending(o => o.CreatedAt) : query.OrderBy(o => o.CreatedAt),
            "totalamount" or "total_amount" => desc ? query.OrderByDescending(o => o.TotalAmount) : query.OrderBy(o => o.TotalAmount),
            _ => query.OrderByDescending(o => o.CreatedAt)
        };
    }

    private static OrderDto MapToDto(Order o) =>
        new(
            o.Id,
            o.CustomerId,
            (OrderStatusDto)o.Status,
            o.CreatedAt,
            o.CurrencyCode,
            o.TotalAmount,
            o.Customer?.Name,
            o.LineItems.Select(li => new OrderLineItemDto(
                li.Id, li.OrderId, li.ProductId, li.ProductSku, li.Quantity, li.UnitPrice, li.Quantity * li.UnitPrice)).ToList(),
            o.RowVersion?.Length > 0 ? Convert.ToBase64String(o.RowVersion) : null);
}
