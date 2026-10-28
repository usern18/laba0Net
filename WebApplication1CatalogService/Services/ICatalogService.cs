using CatalogService.Dtos;

namespace CatalogService.Services;

public interface ICatalogService
{
    Task<PagedItemsResponse> GetItemsAsync(int page, int pageSize, string? search, int? categoryId, string sortBy, bool descending, CancellationToken cancellationToken = default);
    Task<ItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ItemDetailsDto?> GetDetailsByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ItemDetailsDto> CreateAsync(CreateItemRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, UpdateItemRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
}
