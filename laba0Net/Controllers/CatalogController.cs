using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using laba0Net.Data;
using laba0Net.Models;

namespace laba0Net.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatalogController : ControllerBase
{
    private readonly AppDbContext _context;
    public CatalogController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _context.Products.ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var product = await _context.Products.FindAsync(id);
        return product != null ? Ok(product) : NotFound();
    }

    [HttpPost]
    public IActionResult Create() => StatusCode(StatusCodes.Status410Gone, new ProblemDetails
    {
        Title = "Catalog ownership has moved to CatalogService",
        Status = StatusCodes.Status410Gone
    });

    [HttpDelete("{id}")]
    public IActionResult Delete(int id) => StatusCode(StatusCodes.Status410Gone, new ProblemDetails
    {
        Title = "Catalog ownership has moved to CatalogService",
        Status = StatusCodes.Status410Gone
    });
}