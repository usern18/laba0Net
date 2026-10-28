using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OrderingService.Configuration;
using OrderingService.Data;
using OrderingService.Services;
using Shared;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Orders")
    ?? throw new InvalidOperationException("ConnectionStrings:Orders must be configured.");

builder.Services.AddDbContext<OrderingDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddHttpClient("Catalog", client =>
{
    var catalogBaseUrl = builder.Configuration["CatalogApi:BaseUrl"]
        ?? throw new InvalidOperationException("CatalogApi:BaseUrl must be configured.");
    client.BaseAddress = new Uri(catalogBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(2);
});
builder.Services.AddScoped<ICatalogClient, CatalogClient>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddExceptionHandler<OrderingExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
    await db.Database.MigrateAsync();
}

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128)
        correlationId = Guid.NewGuid().ToString("N");
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    using var scope = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("RequestCorrelation").BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
    await next();
});
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", async (OrderingDbContext db, IHttpClientFactory clients, IConfiguration configuration,
    ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    var databaseHealthy = false;
    try
    {
        databaseHealthy = await db.Database.CanConnectAsync(cancellationToken);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        logger.LogError(exception, "Ordering database health check failed");
    }
    var catalogHealthy = false;
    try
    {
        using var response = await clients.CreateClient("Catalog").GetAsync(
            configuration["CatalogApi:HealthPath"] ?? "health", cancellationToken);
        catalogHealthy = response.IsSuccessStatusCode;
    }
    catch (HttpRequestException exception)
    {
        logger.LogWarning(exception, "Catalog health check failed");
    }
    catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
    {
        logger.LogWarning(exception, "Catalog health check timed out");
    }

    var healthy = databaseHealthy && catalogHealthy;
    return Results.Json(new { status = healthy ? "Healthy" : "Unhealthy", database = databaseHealthy, catalog = catalogHealthy },
        statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
})
    .WithSummary("Checks database and Catalog service readiness.")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status503ServiceUnavailable);

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
}

app.Run();

/// <summary>Exposes the entry point to the integration test host.</summary>
public partial class Program;
