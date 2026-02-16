using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = "Orders.Read")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _productService.GetPageAsync(search, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("by-sku/{sku}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetBySku(string sku, CancellationToken ct = default)
    {
        var product = await _productService.GetBySkuAsync(sku, ct);
        if (product == null)
            return NotFound();
        return Ok(product);
    }

    [HttpPost]
    [Authorize(Policy = "Orders.Write")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest request, CancellationToken ct = default)
    {
        try
        {
            var product = await _productService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetBySku), new { sku = product.Sku }, product);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
