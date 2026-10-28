using CatalogService.Data;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(CatalogDbContext db, ILogger<HealthController> logger) : ControllerBase
{
    /// <summary>Checks Catalog service readiness and database connectivity.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            var connected = await db.Database.CanConnectAsync(cancellationToken);
            return connected
                ? Ok(new { status = "Healthy", database = "Connected" })
                : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "Unhealthy", database = "Unavailable" });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Catalog database health check failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "Unhealthy", database = "Unavailable" });
        }
    }
}