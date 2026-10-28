using CatalogService.Data;
using CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Repositories;

public sealed class CatalogRepository(CatalogDbContext db) : ICatalogRepository
{
    public async Task<(IReadOnlyList<Item> Items, int TotalCount)> GetItemsPageAsync(
        int page, int pageSize, string? search, int? categoryId, string sortBy, bool descending, CancellationToken cancellationToken = default)
    {
        IQueryable<Item> query = db.Items.Include(item => item.Category).AsNoTracking().Where(item => item.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Title.Contains(term) || (item.Description != null && item.Description.Contains(term)));
        }
        if (categoryId.HasValue)
            query = query.Where(item => item.CategoryId == categoryId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var orderedQuery = (sortBy.ToLowerInvariant(), descending) switch
        {
            ("title", false) => query.OrderBy(item => item.Title),
            ("title", true) => query.OrderByDescending(item => item.Title),
            ("price", false) => query.OrderBy(item => item.Price),
            ("price", true) => query.OrderByDescending(item => item.Price),
            ("createdat", false) => query.OrderBy(item => item.CreatedAt),
            ("createdat", true) => query.OrderByDescending(item => item.CreatedAt),
            _ => query.OrderByDescending(item => item.CreatedAt)
        };
        var items = await orderedQuery.ThenBy(item => item.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public Task<Item?> GetActiveItemAsync(int id, CancellationToken cancellationToken = default) =>
        db.Items.AsNoTracking().Include(item => item.Category).SingleOrDefaultAsync(item => item.Id == id && item.IsActive, cancellationToken);

    public Task<Item?> GetItemAsync(int id, CancellationToken cancellationToken = default) =>
        db.Items.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default) =>
        db.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken);

    public Task<Category?> GetCategoryAsync(int categoryId, CancellationToken cancellationToken = default) =>
        db.Categories.AsNoTracking().SingleOrDefaultAsync(category => category.Id == categoryId, cancellationToken);

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        await db.Categories.AsNoTracking().OrderBy(category => category.Name).ToListAsync(cancellationToken);

    public void Add(Item item) => db.Items.Add(item);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
