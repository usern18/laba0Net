using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderingService.Dtos;
using OrderingService.Models;
using OrderingService.Services;

namespace OrderingService.Controllers;

/// <summary>Creates and manages user orders.</summary>
[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrdersApiController(IOrderService orders, ILogger<OrdersApiController> logger) : ControllerBase
{
    /// <summary>Lists the caller's orders, with optional status filtering and pagination. Admins can see every order.</summary>
    /// <param name="page">One-based page number. Defaults to 1.</param>
    /// <param name="pageSize">Number of orders per page, from 1 through 100. Defaults to 20.</param>
    /// <param name="status">Optional status filter: Pending or Cancelled.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType<PagedOrdersResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] OrderStatus? status = null, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."]
            }));
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        return Ok(await orders.GetPageAsync(userId, IsAdmin, page, pageSize, status, cancellationToken));
    }

    /// <summary>Gets an order owned by the caller. A non-owned order is reported as not found.</summary>
    /// <param name="id">Order identifier.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        var order = await orders.GetByIdAsync(id, userId, IsAdmin, cancellationToken);
        return order is null ? NotFound(new ProblemDetails { Title = "Order not found", Status = StatusCodes.Status404NotFound }) : Ok(order);
    }

    /// <summary>Creates an order after verifying the catalog item. Optional Idempotency-Key prevents duplicate creation.</summary>
    /// <param name="request">Catalog product and positive quantity. User identity is always taken from the JWT.</param>
    /// <param name="cancellationToken">Request cancellation token propagated to Catalog and the database.</param>
    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (idempotencyKey is { Length: > 128 } || idempotencyKey is not null && string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["Idempotency-Key must contain 1 to 128 non-whitespace characters."]
            }));

        Order? order;
        bool conflict;
        try
        {
            (order, conflict) = await orders.CreateAsync(userId, request, idempotencyKey, cancellationToken);
        }
        catch (CatalogUnavailableException exception)
        {
            logger.LogWarning(exception, "Catalog service failed while creating order for user {UserId}", userId);
            Response.Headers["Retry-After"] = "1";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Catalog service is unavailable",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Catalog service is unreachable while creating order for user {UserId}", userId);
            Response.Headers["Retry-After"] = "1";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Catalog service is unavailable",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Catalog service timed out while creating order for user {UserId}", userId);
            Response.Headers["Retry-After"] = "1";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Catalog service is unavailable",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
        if (conflict)
            return Conflict(new ProblemDetails { Title = "Idempotency-Key was already used for a different order", Status = StatusCodes.Status409Conflict });
        if (order is null)
            return BadRequest(new ProblemDetails { Title = "Catalog item does not exist", Status = StatusCodes.Status400BadRequest });

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, OrderResponse.FromModel(order));
    }

    /// <summary>Cancels an order. Repeating the request is safe and returns the same success status.</summary>
    /// <param name="id">Order identifier.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        return await orders.CancelAsync(id, userId, IsAdmin, cancellationToken)
            ? NoContent()
            : NotFound(new ProblemDetails { Title = "Order not found", Status = StatusCodes.Status404NotFound });
    }

    private bool IsAdmin => User.IsInRole("Admin");

    private bool TryGetUserId(out int userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (int.TryParse(value, out userId) && userId > 0)
            return true;
        logger.LogWarning("Authenticated request has no valid user identifier claim");
        userId = 0;
        return false;
    }
}
