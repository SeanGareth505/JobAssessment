using Backend.Models;

namespace Backend.Services;

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> GetPageAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<CustomerDto?> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default);
}
