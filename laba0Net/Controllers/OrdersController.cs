using System.Net;
using System.Net.Http.Json;
using laba0Net.Data;
using laba0Net.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace laba0Net.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController(AppDbContext context, IHttpClientFactory httpClientFactory, ILogger<OrdersController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await context.Orders.AsNoTracking().ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("Catalog");
        HttpResponseMessage catalogResponse;
        try
        {
            catalogResponse = await client.GetAsync($"api/v1/catalog/items/{request.ProductId}", cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Catalog service request failed while creating an order");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Catalog service is unavailable",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }

        using (catalogResponse)
        {
            if (catalogResponse.StatusCode == HttpStatusCode.NotFound)
                return BadRequest(new ProblemDetails { Title = "Catalog item does not exist", Status = StatusCodes.Status400BadRequest });
            if (!catalogResponse.IsSuccessStatusCode)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Title = "Catalog service could not provide the item",
                    Status = StatusCodes.Status503ServiceUnavailable
                });

            var item = await catalogResponse.Content.ReadFromJsonAsync<CatalogItemResponse>(cancellationToken: cancellationToken);
            if (item is null || !item.IsActive)
                return BadRequest(new ProblemDetails { Title = "Catalog item is unavailable", Status = StatusCodes.Status400BadRequest });

            var order = new Order
            {
                UserId = request.UserId,
                ProductId = item.Id,
                ProductName = item.Title,
                Quantity = request.Quantity,
                TotalPrice = item.Price * request.Quantity
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync(cancellationToken);
            return Ok(order);
        }
    }

    private sealed record CatalogItemResponse(int Id, string Title, decimal Price, bool IsActive);
}