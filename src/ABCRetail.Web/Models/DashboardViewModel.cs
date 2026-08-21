// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

namespace ABCRetail.Web.Models;

/// <summary>
/// Figures shown on the landing page, read live from the storage account so the
/// dashboard reflects what is actually persisted in Azure rather than cached counts.
/// </summary>
public class DashboardViewModel
{
    public int CustomerCount { get; set; }

    public int ProductCount { get; set; }

    public int ImageCount { get; set; }

    public double InventoryValue { get; set; }

    public string StorageAccountName { get; set; } = string.Empty;

    public int OrderCount { get; set; }

    public int PendingOrders { get; set; }

    public int QueueDepth { get; set; }

    public int LogFileCount { get; set; }

    public long ShareBytes { get; set; }

    public IReadOnlyList<Order> RecentOrders { get; set; } = [];

    public IReadOnlyList<Product> RecentProducts { get; set; } = [];

    public IReadOnlyList<CustomerProfile> RecentCustomers { get; set; } = [];
}
