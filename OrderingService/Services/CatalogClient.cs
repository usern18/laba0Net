using System.Net;
using System.Net.Http.Json;

namespace OrderingService.Services;

internal sealed class CatalogClient(IHttpClientFactory httpClientFactory) : ICatalogClient
{
    public async Task<CatalogItem?> GetItemAsync(int id, CancellationToken cancellationToken)
    {
        using var response = await httpClientFactory.CreateClient("Catalog")
            .GetAsync($"api/v1/catalog/items/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            throw new CatalogUnavailableException($"Catalog returned HTTP {(int)response.StatusCode}.");

        var item = await response.Content.ReadFromJsonAsync<CatalogItemResponse>(cancellationToken: cancellationToken)
            ?? throw new CatalogUnavailableException("Catalog returned an invalid item response.");
        return new CatalogItem(item.Id, item.Title, item.Price, item.IsActive);
    }

    private sealed record CatalogItemResponse(int Id, string Title, decimal Price, bool IsActive);
}

internal sealed class CatalogUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
