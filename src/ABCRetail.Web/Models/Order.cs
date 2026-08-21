// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using System.ComponentModel.DataAnnotations;
using Azure;
using Azure.Data.Tables;

namespace ABCRetail.Web.Models;

/// <summary>
/// A customer order held in the Orders table. An order is created in the Submitted state
/// and only advances once its message has been taken off the order-processing queue, so
/// the queue is what actually drives the workflow rather than merely recording it.
/// </summary>
public class Order : ITableEntity
{
    public const string Partition = "ORDER";

    public string PartitionKey { get; set; } = Partition;

    public string RowKey { get; set; } = Guid.NewGuid().ToString();

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    /// <summary>Human readable reference shown to the customer, for example ORD-000004.</summary>
    [Display(Name = "Order number")]
    public string OrderNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a customer.")]
    [Display(Name = "Customer")]
    public string CustomerRowKey { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a product.")]
    [Display(Name = "Product")]
    public string ProductKey { get; set; } = string.Empty;

    public string ProductCategory { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    [Range(1, 500, ErrorMessage = "Order at least one unit.")]
    [Display(Name = "Quantity")]
    public int Quantity { get; set; } = 1;

    public double UnitPrice { get; set; }

    public double TotalPrice { get; set; }

    /// <summary>Submitted, Processing or Completed.</summary>
    public string Status { get; set; } = OrderStatus.Submitted;

    [Display(Name = "Placed")]
    public DateTimeOffset PlacedOn { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ProcessedOn { get; set; }

    public bool IsComplete => Status == OrderStatus.Completed;

    /// <summary>
    /// The row key half of <see cref="ProductKey"/>. A product's identity spans its category
    /// and row key, so the two are stored together separated by a pipe.
    /// </summary>
    public string ProductRowKey =>
        ProductKey.Contains('|') ? ProductKey.Split('|', 2)[1] : ProductKey;
}

/// <summary>The three states an order moves through as its queue message is handled.</summary>
public static class OrderStatus
{
    public const string Submitted = "Submitted";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
}
