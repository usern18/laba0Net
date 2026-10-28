using CatalogService.Models;

namespace CatalogService.Repositories;

public interface ICatalogRepository
{
    Task<(IReadOnlyList<Item> Items, int TotalCount)> GetItemsPageAsync(int page, int pageSize, string? search, int? categoryId, string sortBy, bool descending, CancellationToken cancellationToken = default);
    Task<Item?> GetActiveItemAsync(int id, CancellationToken cancellationToken = default);
    Task<Item?> GetItemAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<Category?> GetCategoryAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    void Add(Item item);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
