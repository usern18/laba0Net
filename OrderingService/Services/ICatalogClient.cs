namespace OrderingService.Services;

/// <summary>Catalog item data needed when creating an order.</summary>
/// <param name="Id">Catalog identifier.</param>
/// <param name="Title">Catalog title snapshot.</param>
/// <param name="Price">Current unit price.</param>
/// <param name="IsActive">Whether the item can be ordered.</param>
public sealed record CatalogItem(int Id, string Title, decimal Price, bool IsActive);

/// <summary>Network boundary for Catalog lookups.</summary>
public interface ICatalogClient
{
    /// <summary>Gets one item within the caller's cancellation and HTTP timeout budget.</summary>
    Task<CatalogItem?> GetItemAsync(int id, CancellationToken cancellationToken);
}
