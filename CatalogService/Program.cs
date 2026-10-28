using CatalogService.Configuration;
using CatalogService.Data;
using CatalogService.Repositories;
using CatalogService.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Shared;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default must be configured.");

builder.Services.AddDbContext<CatalogDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
builder.Services.AddScoped<ICatalogService, CatalogService.Services.CatalogService>();
builder.Services.AddExceptionHandler<CatalogExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.Run();