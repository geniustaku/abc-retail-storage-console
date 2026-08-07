// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using System.ComponentModel.DataAnnotations;
using Azure;
using Azure.Data.Tables;

namespace ABCRetail.Web.Models;

/// <summary>
/// A catalogue item held in the Products table in Azure Table Storage, with its
/// image stored separately in Blob Storage and referenced here by URL.
/// </summary>
/// <remarks>
/// Category is used as the partition key. The catalogue is browsed one category at
/// a time far more often than it is read whole, so this turns the dominant query
/// into a single-partition scan instead of a full table scan, and it spreads writes
/// across partition servers during bulk catalogue loads.
/// </remarks>
public class Product : ITableEntity
{
    public static readonly string[] Categories =
    [
        "Electronics",
        "Home and Living",
        "Apparel",
        "Groceries",
        "Sport and Outdoor"
    ];

    [Required(ErrorMessage = "Select a category.")]
    [Display(Name = "Category")]
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = Guid.NewGuid().ToString();

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    [Required(ErrorMessage = "A product name is required.")]
    [StringLength(100)]
    [Display(Name = "Product name")]
    public string ProductName { get; set; } = string.Empty;

    [StringLength(600)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    // Table Storage supports no decimal type, so prices are persisted as Double and
    // formatted to two places at the view layer. Anything doing real money arithmetic
    // would need to convert to decimal on read.
    [Range(0.01, 1_000_000, ErrorMessage = "Enter a price greater than zero.")]
    [Display(Name = "Price (ZAR)")]
    public double Price { get; set; }

    [Range(0, 100_000, ErrorMessage = "Stock cannot be negative.")]
    [Display(Name = "Stock on hand")]
    public int StockLevel { get; set; }

    [Display(Name = "Image URL")]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>Blob name retained so the image can be replaced or removed later.</summary>
    public string ImageBlobName { get; set; } = string.Empty;

    [Display(Name = "Added")]
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);

    public string StockStatus => StockLevel switch
    {
        0 => "Out of stock",
        < 10 => "Low stock",
        _ => "In stock"
    };
}
