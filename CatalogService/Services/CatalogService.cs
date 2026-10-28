using CatalogService.Dtos;
using CatalogService.Models;
using CatalogService.Repositories;

namespace CatalogService.Services;

public sealed class CatalogService(ICatalogRepository repository, ILogger<CatalogService> logger) : ICatalogService
{
    public async Task<PagedItemsResponse> GetItemsAsync(int page, int pageSize, string? search, int? categoryId, string sortBy, bool descending, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await repository.GetItemsPageAsync(page, pageSize, search, categoryId, sortBy, descending, cancellationToken);
        return new PagedItemsResponse
        {
            Items = items.Select(ToDto).ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await repository.GetActiveItemAsync(id, cancellationToken);
        return item is null ? null : ToDto(item);
    }

    public async Task<ItemDetailsDto?> GetDetailsByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await repository.GetActiveItemAsync(id, cancellationToken);
        return item is null ? null : ToDetails(item);
    }

    public async Task<ItemDetailsDto> CreateAsync(CreateItemRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new CatalogValidationException("title", "Title must not be blank.");
        if (!await repository.CategoryExistsAsync(request.CategoryId, cancellationToken))
            throw new CatalogValidationException("categoryId", "Category does not exist.");

        var item = new Item
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            CategoryId = request.CategoryId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        repository.Add(item);
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created catalog item {ItemId}", item.Id);
        item.Category = await repository.GetCategoryAsync(item.CategoryId, cancellationToken)
            ?? throw new InvalidOperationException("The selected catalog category disappeared during item creation.");
        return ToDetails(item);
    }

    public async Task<bool> UpdateAsync(int id, UpdateItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await repository.GetItemAsync(id, cancellationToken);
        if (item is null)
            return false;
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new CatalogValidationException("title", "Title must not be blank.");
        if (!await repository.CategoryExistsAsync(request.CategoryId, cancellationToken))
            throw new CatalogValidationException("categoryId", "Category does not exist.");

        item.Title = request.Title.Trim();
        item.Description = request.Description?.Trim();
        item.Price = request.Price;
        item.StockQuantity = request.StockQuantity;
        item.CategoryId = request.CategoryId;
        item.IsActive = request.IsActive;
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated catalog item {ItemId}", item.Id);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await repository.GetItemAsync(id, cancellationToken);
        if (item is null)
            return false;
        item.IsActive = false;
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Soft-deleted catalog item {ItemId}", item.Id);
        return true;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        (await repository.GetCategoriesAsync(cancellationToken)).Select(category => new CategoryDto { Id = category.Id, Name = category.Name }).ToArray();

    private static ItemDto ToDto(Item item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Description = item.Description,
        Price = item.Price,
        StockQuantity = item.StockQuantity,
        CategoryId = item.CategoryId,
        CategoryName = item.Category.Name,
        IsActive = item.IsActive,
        CreatedAt = item.CreatedAt
    };

    private static ItemDetailsDto ToDetails(Item item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Description = item.Description,
        Price = item.Price,
        StockQuantity = item.StockQuantity,
        CategoryId = item.CategoryId,
        CategoryName = item.Category.Name,
        IsActive = item.IsActive,
        CreatedAt = item.CreatedAt
    };
}

public sealed class CatalogValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
