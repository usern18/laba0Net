using CatalogService.Dtos;
using CatalogService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Controllers;

/// <summary>Exposes the public catalog and administrator-only catalog changes.</summary>
[ApiController]
[Route("api/v1/catalog")]
public sealed class CatalogController(ICatalogService catalog) : ControllerBase
{
    /// <summary>Returns a filtered and paginated page of active catalog items.</summary>
    [HttpGet("items")]
    [AllowAnonymous]
    [ProducesResponseType<PagedItemsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetItems(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] bool desc = true,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue ||
            categoryId is <= 0 || search?.Length > 200 ||
            !new[] { "title", "price", "createdAt" }.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["query"] = ["Use page >= 1, pageSize 1..100, a positive categoryId, search up to 200 characters, and sortBy title, price, or createdAt."]
            }));
        }

        return Ok(await catalog.GetItemsAsync(page, pageSize, search, categoryId, sortBy, desc, cancellationToken));
    }

    /// <summary>Returns one active item by identifier.</summary>
    [HttpGet("items/{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<ItemDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await catalog.GetDetailsByIdAsync(id, cancellationToken);
        return item is null ? NotFound(new ProblemDetails { Title = "Item not found", Status = StatusCodes.Status404NotFound }) : Ok(item);
    }

    /// <summary>Creates a catalog item. Requires the Admin role.</summary>
    [HttpPost("items")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<ItemDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateItemRequest request, CancellationToken cancellationToken)
    {
        var item = await catalog.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    /// <summary>Replaces a catalog item. Requires the Admin role.</summary>
    [HttpPut("items/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateItemRequest request, CancellationToken cancellationToken)
    {
        return await catalog.UpdateAsync(id, request, cancellationToken)
            ? NoContent()
            : NotFound(new ProblemDetails { Title = "Item not found", Status = StatusCodes.Status404NotFound });
    }

    /// <summary>Soft-deletes a catalog item. Requires the Admin role.</summary>
    [HttpDelete("items/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        return await catalog.DeleteAsync(id, cancellationToken)
            ? NoContent()
            : NotFound(new ProblemDetails { Title = "Item not found", Status = StatusCodes.Status404NotFound });
    }

    /// <summary>Returns all catalog categories.</summary>
    [HttpGet("categories")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken) =>
        Ok(await catalog.GetCategoriesAsync(cancellationToken));
}