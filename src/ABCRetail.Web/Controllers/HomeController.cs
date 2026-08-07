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
    private readonly IBlobStorageService _blobs;
    private readonly IConfiguration _configuration;

    public HomeController(
        ITableStorageService<CustomerProfile> customers,
        ITableStorageService<Product> products,
        IBlobStorageService blobs,
        IConfiguration configuration)
    {
        _customers = customers;
        _products = products;
        _blobs = blobs;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // The three reads hit independent services, so they are issued together rather
        // than one after the other. On the free App Service tier that difference is
        // noticeable on a cold start.
        var customersTask = _customers.GetAllAsync(cancellationToken: cancellationToken);
        var productsTask = _products.GetAllAsync(cancellationToken: cancellationToken);
        var imageCountTask = _blobs.CountAsync(cancellationToken);

        await Task.WhenAll(customersTask, productsTask, imageCountTask);

        var customers = await customersTask;
        var products = await productsTask;

        var model = new DashboardViewModel
        {
            CustomerCount = customers.Count,
            ProductCount = products.Count,
            ImageCount = await imageCountTask,
            InventoryValue = products.Sum(p => p.Price * p.StockLevel),
            StorageAccountName = ResolveAccountName(),
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
