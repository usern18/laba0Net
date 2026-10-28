using System.ComponentModel.DataAnnotations;

namespace CatalogService.Dtos;

/// <summary>Public catalog item representation used in list results.</summary>
public class ItemDto
{
    /// <summary>Stable catalog item identifier.</summary>
    public int Id { get; set; }
    /// <summary>Item title.</summary>
    public string Title { get; set; } = null!;
    /// <summary>Optional item description.</summary>
    public string? Description { get; set; }
    /// <summary>Current price.</summary>
    public decimal Price { get; set; }
    /// <summary>Current available stock quantity.</summary>
    public int StockQuantity { get; set; }
    /// <summary>Owning category identifier.</summary>
    public int CategoryId { get; set; }
    /// <summary>Owning category name.</summary>
    public string? CategoryName { get; set; }
    /// <summary>Whether the item is currently available.</summary>
    public bool IsActive { get; set; }
    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>Detailed public catalog item.</summary>
public sealed class ItemDetailsDto : ItemDto
{
}

/// <summary>Fields accepted when creating a catalog item.</summary>
public class CreateItemRequest
{
    /// <summary>Required title, up to 200 characters.</summary>
    [Required, MaxLength(200)]
    public string Title { get; set; } = null!;
    /// <summary>Optional description, up to 2,000 characters.</summary>
    [MaxLength(2000)]
    public string? Description { get; set; }
    /// <summary>Non-negative item price.</summary>
    [Range(0, 1_000_000)]
    public decimal Price { get; set; }
    /// <summary>Initial stock quantity.</summary>
    [Range(0, 1_000_000)]
    public int StockQuantity { get; set; }
    /// <summary>Existing category identifier.</summary>
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}

/// <summary>Fields accepted when replacing a catalog item.</summary>
public class UpdateItemRequest
{
    /// <summary>Required replacement title, up to 200 characters.</summary>
    [Required, MaxLength(200)]
    public string Title { get; set; } = null!;
    /// <summary>Optional replacement description, up to 2,000 characters.</summary>
    [MaxLength(2000)]
    public string? Description { get; set; }
    /// <summary>Replacement non-negative price.</summary>
    [Range(0, 1_000_000)]
    public decimal Price { get; set; }
    /// <summary>Replacement stock quantity.</summary>
    [Range(0, 1_000_000)]
    public int StockQuantity { get; set; }
    /// <summary>Existing category identifier.</summary>
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
    /// <summary>Whether the item is available after the update.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>Public category reference.</summary>
public class CategoryDto
{
    /// <summary>Category identifier.</summary>
    public int Id { get; set; }
    /// <summary>Category display name.</summary>
    public string Name { get; set; } = null!;
}

/// <summary>Catalog items and pagination metadata.</summary>
public class PagedItemsResponse
{
    /// <summary>Items in the requested page.</summary>
    public IReadOnlyList<ItemDto> Items { get; set; } = Array.Empty<ItemDto>();
    /// <summary>One-based page number.</summary>
    public int Page { get; set; }
    /// <summary>Maximum number of items in this page.</summary>
    public int PageSize { get; set; }
    /// <summary>Total matching active items before pagination.</summary>
    public int TotalCount { get; set; }
}