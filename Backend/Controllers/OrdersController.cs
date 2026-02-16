using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = "Orders.Read")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetPage(
        [FromQuery] Guid? customerId,
        [FromQuery] OrderStatusDto? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sort = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _orderService.GetPageAsync(customerId, status, page, pageSize, sort, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, [FromHeader(Name = "If-None-Match")] string? ifNoneMatch, CancellationToken ct)
    {
        var order = await _orderService.GetByIdAsync(id, ct);
        if (order == null)
            return NotFound();
        var etag = order.Etag != null ? $"\"{order.Etag}\"" : null;
        if (etag != null && ifNoneMatch?.Trim().Trim('"') == order.Etag)
            return StatusCode(StatusCodes.Status304NotModified);
        if (etag != null)
            Response.Headers.Append("ETag", etag);
        return Ok(order);
    }

    [HttpPost]
    [Authorize(Policy = "Orders.Write")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        try
        {
            var order = await _orderService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("Customer not found") || ex.Message.Contains("Currency") ||
                ex.Message.Contains("line item") || ex.Message.Contains("not found") || ex.Message.Contains("Product "))
                return BadRequest(new { message = ex.Message });
            throw;
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Orders.Write")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Update(Guid id, [FromBody] UpdateOrderRequest request, CancellationToken ct = default)
    {
        try
        {
            var order = await _orderService.UpdateAsync(id, request, ct);
            if (order == null)
                return NotFound();
            return Ok(order);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Only Pending") || ex.Message.Contains("line item") ||
            ex.Message.Contains("Currency") || ex.Message.Contains("not found") || ex.Message.Contains("Product "))
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("concurrency"))
        {
            return Conflict(new { message = ex.Message, error = "Concurrency conflict" });
        }
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "Orders.Write")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        try
        {
            var order = await _orderService.UpdateStatusAsync(id, request.Status, idempotencyKey, ct);
            if (order == null)
                return NotFound();
            return Ok(order);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Invalid status transition"))
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("concurrency"))
        {
            return Conflict(new { message = ex.Message, error = "Concurrency conflict" });
        }
    }
}
