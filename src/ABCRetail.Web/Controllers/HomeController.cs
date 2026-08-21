// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using System.Diagnostics;
using ABCRetail.Web.Models;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Web.Controllers;

/// <summary>
/// Landing page. Reads across both storage services so the figures on screen come
/// straight from Azure rather than from anything held in the application.
/// </summary>
public class HomeController : Controller
{
    private readonly ITableStorageService<CustomerProfile> _customers;
    private readonly ITableStorageService<Product> _products;
    private readonly ITableStorageService<Order> _orders;
    private readonly IBlobStorageService _blobs;
    private readonly IQueueStorageService _queues;
    private readonly IFileShareService _files;
    private readonly IConfiguration _configuration;

    public HomeController(
        ITableStorageService<CustomerProfile> customers,
        ITableStorageService<Product> products,
        ITableStorageService<Order> orders,
        IBlobStorageService blobs,
        IQueueStorageService queues,
        IFileShareService files,
        IConfiguration configuration)
    {
        _customers = customers;
        _products = products;
        _orders = orders;
        _blobs = blobs;
        _queues = queues;
        _files = files;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Every read below hits a different storage service, so they are issued together
        // rather than one after another. On the free App Service tier that difference is
        // clearly noticeable on a cold start.
        var customersTask = _customers.GetAllAsync(cancellationToken: cancellationToken);
        var productsTask = _products.GetAllAsync(cancellationToken: cancellationToken);
        var ordersTask = _orders.GetAllAsync(Order.Partition, cancellationToken);
        var imageCountTask = _blobs.CountAsync(cancellationToken);
        var logFilesTask = _files.ListAsync(IFileShareService.LogsDirectory, cancellationToken);
        var depthTask = Task.WhenAll(QueueNames.All.Select(q => _queues.GetDepthAsync(q, cancellationToken)));

        await Task.WhenAll(customersTask, productsTask, ordersTask, imageCountTask, logFilesTask, depthTask);

        var customers = await customersTask;
        var products = await productsTask;
        var orders = await ordersTask;
        var logFiles = await logFilesTask;

        var model = new DashboardViewModel
        {
            CustomerCount = customers.Count,
            ProductCount = products.Count,
            ImageCount = await imageCountTask,
            InventoryValue = products.Sum(p => p.Price * p.StockLevel),
            StorageAccountName = ResolveAccountName(),
            OrderCount = orders.Count,
            PendingOrders = orders.Count(o => !o.IsComplete),
            QueueDepth = (await depthTask).Sum(),
            LogFileCount = logFiles.Count,
            ShareBytes = logFiles.Sum(f => f.SizeBytes),
            RecentOrders = orders.OrderByDescending(o => o.PlacedOn).Take(5).ToList(),
            RecentProducts = products.OrderByDescending(p => p.CreatedOn).Take(4).ToList(),
            RecentCustomers = customers.OrderByDescending(c => c.RegisteredOn).Take(5).ToList()
        };

        return View(model);
    }

    public IActionResult About() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }

    /// <summary>
    /// Pulls the account name out of the connection string for display, so the page can
    /// name the storage account it is reading from without exposing the key.
    /// </summary>
    private string ResolveAccountName()
    {
        var connectionString = _configuration.GetConnectionString("AzureStorage") ?? string.Empty;

        var segment = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(part => part.StartsWith("AccountName=", StringComparison.OrdinalIgnoreCase));

        return segment?["AccountName=".Length..] ?? "storage account";
    }
}
