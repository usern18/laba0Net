using System.Diagnostics;
using System.Net;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

builder.Services.AddOcelot(builder.Configuration);
builder.Services.AddHttpClient();
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

app.Use(async (context, next) =>
{
    const string header = "X-Correlation-Id";
    if (!context.Request.Headers.TryGetValue(header, out var cid) || string.IsNullOrWhiteSpace(cid))
    {
        cid = Guid.NewGuid().ToString();
        context.Request.Headers[header] = cid;
    }
    context.Response.Headers[header] = cid;
    await next();
});

app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    await next();
    sw.Stop();
    var cid = context.Response.Headers["X-Correlation-Id"];
    Console.WriteLine($"[GATEWAY LOG] {context.Request.Method} {context.Request.Path} -> {context.Response.StatusCode} ({sw.ElapsedMilliseconds}ms) [CId: {cid}]");
});

app.MapWhen(ctx => ctx.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase), branch =>
{
    branch.Run(async context =>
    {
        var factory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        var services = new Dictionary<string, string>
        {
            ["auth"] = builder.Configuration["HealthChecks:AuthUrl"] ?? "http://localhost:5001/health",
            ["catalog"] = builder.Configuration["HealthChecks:CatalogUrl"] ?? "http://localhost:5002/health",
            ["ordering"] = builder.Configuration["HealthChecks:OrderingUrl"] ?? "http://localhost:5003/health"
        };
        var statuses = new Dictionary<string, string>();
        foreach (var (name, url) in services)
        {
            try
            {
                using var response = await factory.CreateClient().GetAsync(url, context.RequestAborted);
                statuses[name] = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy";
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                statuses[name] = "Unhealthy";
            }
        }

        var healthy = statuses.Values.All(status => status == "Healthy");
        context.Response.StatusCode = healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { status = healthy ? "Healthy" : "Unhealthy", services = statuses }, context.RequestAborted);
    });
});

await app.UseOcelot();

await app.RunAsync();