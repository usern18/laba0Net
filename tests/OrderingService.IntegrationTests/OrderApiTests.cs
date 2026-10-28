using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using OrderingService.Data;
using OrderingService.Dtos;
using OrderingService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OrderingService.IntegrationTests;

public sealed class OrderApiTests
{
    private const string SigningKey = "integration-test-signing-key-at-least-32-bytes";

    [Fact]
    public async Task Create_ValidOrder_Returns201LocationAndCatalogSnapshot()
    {
        using var factory = await CreateFactoryAsync();
        using var client = AuthorizedClient(factory, 1);
        using var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(1, 2));
        var responseBody = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(responseBody);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("Квиток на подію", body.RootElement.GetProperty("productName").GetString());
        Assert.Equal(450m, body.RootElement.GetProperty("unitPrice").GetDecimal());
        Assert.Equal(900m, body.RootElement.GetProperty("totalPrice").GetDecimal());
        Assert.Equal(1, body.RootElement.GetProperty("userId").GetInt32());
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Orders.CountAsync());
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(1, 1));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_WhenCatalogItemDoesNotExist_Returns400ProblemDetails()
    {
        using var factory = await CreateFactoryAsync();
        factory.Catalog.StatusCode = HttpStatusCode.NotFound;
        using var client = AuthorizedClient(factory, 1);
        using var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(999999, 1));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Catalog item does not exist", body.RootElement.GetProperty("title").GetString());
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Create_WithNonPositiveQuantity_ReturnsValidationProblem()
    {
        using var factory = await CreateFactoryAsync();
        using var client = AuthorizedClient(factory, 1);
        using var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(1, 0));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("Quantity", out var errors));
        Assert.Equal("The field Quantity must be between 1 and 2147483647.", errors[0].GetString());
    }

    [Fact]
    public async Task Create_RepeatedIdempotencyKey_ReturnsSameOrderAndCreatesOneRow()
    {
        using var factory = await CreateFactoryAsync();
        using var client = AuthorizedClient(factory, 1);
        using var firstRequest = CreateRequest("same-order-key");
        using var first = await client.SendAsync(firstRequest);
        var firstBody = await first.Content.ReadAsStringAsync();
        using var secondRequest = CreateRequest("same-order-key");
        using var second = await client.SendAsync(secondRequest);

        var secondBody = await second.Content.ReadAsStringAsync();
        using var firstJson = JsonDocument.Parse(firstBody);
        using var secondJson = JsonDocument.Parse(secondBody);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(firstJson.RootElement.GetProperty("id").GetInt32(), secondJson.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(firstJson.RootElement.GetProperty("totalPrice").GetDecimal(), secondJson.RootElement.GetProperty("totalPrice").GetDecimal());
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Orders.CountAsync());
    }

    [Fact]
    public async Task Create_ReusingIdempotencyKeyForDifferentPayload_Returns409()
    {
        using var factory = await CreateFactoryAsync();
        using var client = AuthorizedClient(factory, 1);
        using var firstRequest = CreateRequest("conflicting-order-key");
        using var first = await client.SendAsync(firstRequest);
        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new CreateOrderRequest(1, 2))
        };
        secondRequest.Headers.Add("Idempotency-Key", "conflicting-order-key");
        using var second = await client.SendAsync(secondRequest);
        using var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(409, body.RootElement.GetProperty("status").GetInt32());
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Orders.CountAsync());
    }

    [Fact]
    public async Task GetById_OwnOrder_ReturnsItsFields()
    {
        using var factory = await CreateFactoryAsync();
        using var client = AuthorizedClient(factory, 1);
        using var created = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(1, 1));
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdBody.RootElement.GetProperty("id").GetInt32();
        using var response = await client.GetAsync($"/api/orders/{id}");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(id, body.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("Pending", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetById_OtherUsersOrder_Returns404WithoutDisclosure()
    {
        using var factory = await CreateFactoryAsync();
        using var ownerClient = AuthorizedClient(factory, 1);
        using var created = await ownerClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest(1, 1));
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdBody.RootElement.GetProperty("id").GetInt32();
        using var otherClient = AuthorizedClient(factory, 2);
        using var response = await otherClient.GetAsync($"/api/orders/{id}");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Order not found", body.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task CancelOrder_ChangesStatusAndIsSafeToRepeat()
    {
        using var factory = await CreateFactoryAsync();
        using var client = AuthorizedClient(factory, 1);
        using var created = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(1, 1));
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdBody.RootElement.GetProperty("id").GetInt32();
        using var cancelled = await client.PostAsync($"/api/orders/{id}/cancel", null);
        using var repeated = await client.PostAsync($"/api/orders/{id}/cancel", null);
        using var fetched = await client.GetAsync($"/api/orders/{id}");
        using var order = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        Assert.Equal(string.Empty, await cancelled.Content.ReadAsStringAsync());
        Assert.Equal(string.Empty, await repeated.Content.ReadAsStringAsync());
        Assert.Equal("Cancelled", order.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Create_WhenCatalogIsUnavailable_Returns503ProblemDetails()
    {
        using var factory = await CreateFactoryAsync();
        factory.Catalog.StatusCode = HttpStatusCode.ServiceUnavailable;
        using var client = AuthorizedClient(factory, 1);
        using var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(1, 1));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Catalog service is unavailable", body.RootElement.GetProperty("title").GetString());
        Assert.Equal(503, body.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task OpenApi_ContainsBearerSecurityAndRequestAndResponseExamples()
    {
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var post = document.RootElement.GetProperty("paths").GetProperty("/api/orders").GetProperty("post");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("bearer", document.RootElement.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.Equal(1, post.GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
            .GetProperty("example").GetProperty("productId").GetInt32());
        Assert.Equal("Квиток на подію", post.GetProperty("responses").GetProperty("201")
            .GetProperty("content").GetProperty("application/json").GetProperty("example")
            .GetProperty("productName").GetString());
        Assert.Equal(503, post.GetProperty("responses").GetProperty("503")
            .GetProperty("content").GetProperty("application/json").GetProperty("example")
            .GetProperty("status").GetInt32());
        Assert.NotEmpty(post.GetProperty("security").EnumerateArray());
        Assert.Contains(post.GetProperty("parameters").EnumerateArray(), parameter =>
            parameter.GetProperty("name").GetString() == "Idempotency-Key" && parameter.GetProperty("in").GetString() == "header");
    }

    [Fact]
    public async Task Create_WhenCatalogTimesOut_Returns503ProblemDetails()
    {
        using var factory = await CreateFactoryAsync();
        factory.Catalog.ResponseDelay = TimeSpan.FromSeconds(3);
        using var client = AuthorizedClient(factory, 1);
        using var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(1, 1));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Catalog service is unavailable", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("1", response.Headers.RetryAfter?.Delta?.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task GetAll_FiltersAndPaginatesTheCallersOrders()
    {
        using var factory = await CreateFactoryAsync();
        using var client = AuthorizedClient(factory, 1);
        using var first = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(1, 1));
        using var second = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(1, 1));
        using var page = await client.GetAsync("/api/orders?page=1&pageSize=1&status=Pending");
        using var body = JsonDocument.Parse(await page.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(1, body.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(2, body.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("page").GetInt32());
    }

    private static async Task<ApiFactory> CreateFactoryAsync()
    {
        var factory = new ApiFactory();
        using var startupClient = factory.CreateClient();
        await factory.InitializeDatabaseAsync();
        return factory;
    }

    private static HttpClient AuthorizedClient(ApiFactory factory, int userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(userId));
        return client;
    }

    private static HttpRequestMessage CreateRequest(string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new CreateOrderRequest(1, 1))
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    private static string CreateToken(int userId)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "User")
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var token = new JwtSecurityToken("OrderingService.Tests", "OrderingService.Tests", claims,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
