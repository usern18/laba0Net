using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderingService.Data;

namespace OrderingService.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private const string TestSigningKey = "integration-test-signing-key-at-least-32-bytes";
    private static readonly string[] TestSettingsKeys =
    [
        "ConnectionStrings__Orders",
        "CatalogApi__BaseUrl",
        "Jwt__Key",
        "Jwt__Issuer",
        "Jwt__Audience"
    ];
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Dictionary<string, string?> _previousEnvironment = TestSettingsKeys
        .ToDictionary(key => key, Environment.GetEnvironmentVariable);
    public StubCatalogHttpClientFactory Catalog { get; } = new();

    public ApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Orders", "Data Source=:memory:");
        Environment.SetEnvironmentVariable("CatalogApi__BaseUrl", "http://catalog.test/");
        Environment.SetEnvironmentVariable("Jwt__Key", TestSigningKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "OrderingService.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "OrderingService.Tests");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrderingDbContext>>();
            services.RemoveAll<OrderingDbContext>();
            services.AddDbContext<OrderingDbContext>(options => options.UseSqlite(_connection));
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(Catalog);
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            foreach (var (key, value) in _previousEnvironment)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}

public sealed class StubCatalogHttpClientFactory : IHttpClientFactory
{
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    public TimeSpan ResponseDelay { get; set; }
    public string ItemJson { get; set; } = "{\"id\":1,\"title\":\"Квиток на подію\",\"price\":450,\"isActive\":true}";

    public HttpClient CreateClient(string name) => new(new StubCatalogHandler(this), disposeHandler: true)
    {
        BaseAddress = new Uri("http://catalog.test/"),
        Timeout = TimeSpan.FromSeconds(2)
    };

    private sealed class StubCatalogHandler(StubCatalogHttpClientFactory owner) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (owner.ResponseDelay > TimeSpan.Zero)
                await Task.Delay(owner.ResponseDelay, cancellationToken);
            var content = owner.StatusCode == HttpStatusCode.OK ? new StringContent(owner.ItemJson, Encoding.UTF8, "application/json") : null;
            return new HttpResponseMessage(owner.StatusCode) { Content = content };
        }
    }
}
