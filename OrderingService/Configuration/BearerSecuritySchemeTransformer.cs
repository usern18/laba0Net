using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace OrderingService.Configuration;

internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            In = ParameterLocation.Header,
            BearerFormat = "JWT"
        };

        foreach (var description in context.DescriptionGroups.SelectMany(group => group.Items))
        {
            var metadata = description.ActionDescriptor.EndpointMetadata;
            var path = "/" + (description.RelativePath ?? string.Empty).Split('?')[0].TrimStart('/');
            if (document.Paths is null || !document.Paths.TryGetValue(path, out var pathItem) || pathItem is null ||
                description.HttpMethod is not { } method ||
                !pathItem.Operations.TryGetValue(new HttpMethod(method), out var operation))
                continue;

            if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any())
                continue;
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
            if (path == "/api/orders" && method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                operation.Parameters ??= [];
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "Idempotency-Key",
                    In = ParameterLocation.Header,
                    Required = false,
                    Description = "Optional per-user key (1-128 characters) that makes retries return the original order."
                });
            }
            AddExamples(path, method, operation);
        }
        return Task.CompletedTask;
    }

    private static void AddExamples(string path, string method, OpenApiOperation operation)
    {
        if (path == "/api/orders" && method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            operation.RequestBody is OpenApiRequestBody requestBody && requestBody.Content.TryGetValue("application/json", out var requestContent))
            requestContent.Example = JsonNode.Parse("{\"productId\":1,\"quantity\":2}");

        foreach (var (status, json) in GetResponseExamples(path, method))
        {
            if (!operation.Responses.TryGetValue(status, out var response) || response is not OpenApiResponse concreteResponse ||
                !concreteResponse.Content.TryGetValue("application/json", out var content))
                continue;
            content.Example = JsonNode.Parse(json);
        }
    }

    private static IEnumerable<(string Status, string Json)> GetResponseExamples(string path, string method)
    {
        if (path == "/api/orders" && method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            yield return ("201", "{\"id\":42,\"userId\":7,\"productId\":1,\"productName\":\"Квиток на подію\",\"quantity\":2,\"totalPrice\":900,\"unitPrice\":450,\"status\":\"Pending\",\"createdAt\":\"2026-10-21T00:00:00Z\"}");
            yield return ("400", "{\"title\":\"Catalog item does not exist\",\"status\":400}");
            yield return ("409", "{\"title\":\"Idempotency-Key was already used for a different order\",\"status\":409}");
            yield return ("503", "{\"title\":\"Catalog service is unavailable\",\"status\":503}");
        }
        else if (path == "/api/orders" && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            yield return ("200", "{\"items\":[{\"id\":42,\"userId\":7,\"productId\":1,\"productName\":\"Квиток на подію\",\"quantity\":2,\"totalPrice\":900,\"unitPrice\":450,\"status\":\"Pending\",\"createdAt\":\"2026-10-21T00:00:00Z\"}],\"page\":1,\"pageSize\":20,\"totalCount\":1}");
            yield return ("400", "{\"title\":\"One or more validation errors occurred.\",\"status\":400,\"errors\":{\"pagination\":[\"Page must be positive and pageSize must be between 1 and 100.\"]}}");
        }
        else if (path == "/api/orders/{id}" && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            yield return ("200", "{\"id\":42,\"userId\":7,\"productId\":1,\"productName\":\"Квиток на подію\",\"quantity\":2,\"totalPrice\":900,\"unitPrice\":450,\"status\":\"Pending\",\"createdAt\":\"2026-10-21T00:00:00Z\"}");
            yield return ("404", "{\"title\":\"Order not found\",\"status\":404}");
        }
        else if (path == "/api/orders/{id}/cancel" && method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            yield return ("404", "{\"title\":\"Order not found\",\"status\":404}");
        }
    }
}
