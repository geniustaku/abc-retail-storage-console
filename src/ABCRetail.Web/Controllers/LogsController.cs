// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using System.Text;
using ABCRetail.Web.Models;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Web.Controllers;

/// <summary>
/// Browses the Azure file share that holds the application's log files, and writes
/// inventory reports to it on demand.
/// </summary>
public class LogsController : Controller
{
    private readonly IFileShareService _files;
    private readonly ITableStorageService<Product> _products;
    private readonly ITableStorageService<CustomerProfile> _customers;
    private readonly ITableStorageService<Order> _orders;
    private readonly IActivityRecorder _activity;

    public LogsController(
        IFileShareService files,
        ITableStorageService<Product> products,
        ITableStorageService<CustomerProfile> customers,
        ITableStorageService<Order> orders,
        IActivityRecorder activity)
    {
        _files = files;
        _products = products;
        _customers = customers;
        _orders = orders;
        _activity = activity;
    }

    public async Task<IActionResult> Index(string? directory, string? file, CancellationToken cancellationToken)
    {
        var logsTask = _files.ListAsync(IFileShareService.LogsDirectory, cancellationToken);
        var exportsTask = _files.ListAsync(IFileShareService.ExportsDirectory, cancellationToken);
        await Task.WhenAll(logsTask, exportsTask);

        var model = new FileShareViewModel
        {
            ShareName = _files.ShareName,
            LogFiles = await logsTask,
            Exports = await exportsTask
        };

        // With nothing selected, the most recent log opens by default. That is almost always
        // the file someone wants, and it means the viewer is never empty on arrival.
        directory ??= IFileShareService.LogsDirectory;
        file ??= model.LogFiles.FirstOrDefault()?.Name;

        if (!string.IsNullOrWhiteSpace(file) && IsKnownDirectory(directory))
        {
            var bytes = await _files.ReadAsync(directory, file, cancellationToken);
            if (bytes is not null)
            {
                model.SelectedPath = $"{directory}/{file}";
                model.SelectedContent = Encoding.UTF8.GetString(bytes)
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => LogLine.Parse(line.TrimEnd('\r')))
                    .Reverse()
                    .ToList();
            }
        }

        return View(model);
    }

    /// <summary>Streams a file straight from the share to the browser.</summary>
    public async Task<IActionResult> Download(string directory, string file, CancellationToken cancellationToken)
    {
        if (!IsKnownDirectory(directory) || string.IsNullOrWhiteSpace(file))
        {
            return BadRequest();
        }

        var bytes = await _files.ReadAsync(directory, file, cancellationToken);
        if (bytes is null)
        {
            return NotFound();
        }

        var contentType = file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? "text/csv"
            : "text/plain";

        return File(bytes, contentType, file);
    }

    /// <summary>
    /// Writes a report to the share as CSV. Three reports are offered because the three
    /// tables answer different operational questions, and each is a separate file on the
    /// share so it can be retrieved by name later.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Export(string report, CancellationToken cancellationToken)
    {
        var (prefix, csv) = report switch
        {
            "customers" => ("customers", await BuildCustomerReportAsync(cancellationToken)),
            "orders" => ("orders", await BuildOrderReportAsync(cancellationToken)),
            _ => ("inventory", await BuildInventoryReportAsync(cancellationToken))
        };

        var fileName = $"{prefix}-{DateTimeOffset.UtcNow:yyyy-MM-dd-HHmmss}.csv";
        await _files.WriteFileAsync(
            IFileShareService.ExportsDirectory,
            fileName,
            Encoding.UTF8.GetBytes(csv),
            cancellationToken);

        await _activity.RecordAsync(
            QueueNames.InventoryManagement,
            "ReportGenerated",
            $"Report {fileName} written to the {_files.ShareName} file share",
            fileName,
            cancellationToken);

        TempData["Message"] = $"{fileName} was written to the exports directory on the file share.";
        return RedirectToAction(nameof(Index), new { directory = IFileShareService.ExportsDirectory, file = fileName });
    }

    private async Task<string> BuildInventoryReportAsync(CancellationToken cancellationToken)
    {
        var products = await _products.GetAllAsync(cancellationToken: cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Category,Product,Price,StockLevel,StockValue,ImageBlobName");

        foreach (var product in products.OrderBy(p => p.PartitionKey).ThenBy(p => p.ProductName))
        {
            csv.AppendLine(Row(
                product.PartitionKey,
                product.ProductName,
                product.Price.ToString("F2"),
                product.StockLevel.ToString(),
                (product.Price * product.StockLevel).ToString("F2"),
                product.ImageBlobName));
        }

        return csv.ToString();
    }

    private async Task<string> BuildCustomerReportAsync(CancellationToken cancellationToken)
    {
        var customers = await _customers.GetAllAsync(CustomerProfile.Partition, cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Surname,FirstName,Email,PhoneNumber,City,PostalCode,Registered");

        foreach (var customer in customers.OrderBy(c => c.Surname).ThenBy(c => c.FirstName))
        {
            csv.AppendLine(Row(
                customer.Surname,
                customer.FirstName,
                customer.Email,
                customer.PhoneNumber,
                customer.City,
                customer.PostalCode,
                customer.RegisteredOn.ToString("yyyy-MM-dd")));
        }

        return csv.ToString();
    }

    private async Task<string> BuildOrderReportAsync(CancellationToken cancellationToken)
    {
        var orders = await _orders.GetAllAsync(Order.Partition, cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("OrderNumber,Customer,Product,Quantity,UnitPrice,Total,Status,Placed,Processed");

        foreach (var order in orders.OrderByDescending(o => o.PlacedOn))
        {
            csv.AppendLine(Row(
                order.OrderNumber,
                order.CustomerName,
                order.ProductName,
                order.Quantity.ToString(),
                order.UnitPrice.ToString("F2"),
                order.TotalPrice.ToString("F2"),
                order.Status,
                order.PlacedOn.ToString("yyyy-MM-dd HH:mm"),
                order.ProcessedOn?.ToString("yyyy-MM-dd HH:mm") ?? ""));
        }

        return csv.ToString();
    }

    /// <summary>
    /// Builds one CSV row. Every field is quoted so a comma inside a product name cannot
    /// shift the remaining columns, and any embedded quote is doubled as CSV requires.
    /// </summary>
    private static string Row(params string[] fields) =>
        string.Join(',', fields.Select(f => $"\"{(f ?? string.Empty).Replace("\"", "\"\"")}\""));

    private static bool IsKnownDirectory(string? directory) =>
        directory is IFileShareService.LogsDirectory or IFileShareService.ExportsDirectory;

}
