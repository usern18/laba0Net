using Microsoft.EntityFrameworkCore;
using laba0Net.Data;
using laba0Net.Models;


var builder = WebApplication.CreateBuilder(args);

// Підключення SQLite бази даних
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
var catalogBaseUrl = builder.Configuration["CatalogApi:BaseUrl"]
    ?? throw new InvalidOperationException("CatalogApi:BaseUrl must be configured.");
builder.Services.AddHttpClient("Catalog", client => client.BaseAddress = new Uri(catalogBaseUrl));

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Ensure the monolith's remaining database exists; Catalog data is now owned by CatalogService.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.Run();