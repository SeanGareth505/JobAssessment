using Backend.Models;

namespace Backend.Services;

public interface IOrderService
{
    Task<PagedResult<OrderDto>> GetPageAsync(Guid? customerId, OrderStatusDto? status, int page, int pageSize, string? sort, CancellationToken ct = default);
    Task<OrderDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<OrderDto?> UpdateAsync(Guid orderId, UpdateOrderRequest request, CancellationToken ct = default);
    Task<OrderDto?> UpdateStatusAsync(Guid orderId, OrderStatusDto status, string? idempotencyKey, CancellationToken ct = default);
}
