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
    private readonly IActivityRecorder _activity;

    public LogsController(
        IFileShareService files,
        ITableStorageService<Product> products,
        IActivityRecorder activity)
    {
        _files = files;
        _products = products;
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

    /// <summary>Writes a snapshot of the catalogue to the share as a CSV report.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var products = await _products.GetAllAsync(cancellationToken: cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Category,Product,Price,StockLevel,StockValue,ImageBlobName");

        foreach (var product in products.OrderBy(p => p.PartitionKey).ThenBy(p => p.ProductName))
        {
            // Quoting every text field keeps a comma inside a product name from shifting the
            // remaining columns, and any embedded quote is doubled as CSV requires.
            csv.AppendLine(string.Join(',',
                Quote(product.PartitionKey),
                Quote(product.ProductName),
                product.Price.ToString("F2"),
                product.StockLevel.ToString(),
                (product.Price * product.StockLevel).ToString("F2"),
                Quote(product.ImageBlobName)));
        }

        var fileName = $"inventory-{DateTimeOffset.UtcNow:yyyy-MM-dd-HHmmss}.csv";
        await _files.WriteFileAsync(
            IFileShareService.ExportsDirectory,
            fileName,
            Encoding.UTF8.GetBytes(csv.ToString()),
            cancellationToken);

        await _activity.RecordAsync(
            QueueNames.InventoryManagement,
            "ReportGenerated",
            $"Inventory report {fileName} written to the {_files.ShareName} file share",
            fileName,
            cancellationToken);

        TempData["Message"] = $"{fileName} was written to the exports directory on the file share.";
        return RedirectToAction(nameof(Index), new { directory = IFileShareService.ExportsDirectory, file = fileName });
    }

    private static bool IsKnownDirectory(string? directory) =>
        directory is IFileShareService.LogsDirectory or IFileShareService.ExportsDirectory;

    private static string Quote(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
