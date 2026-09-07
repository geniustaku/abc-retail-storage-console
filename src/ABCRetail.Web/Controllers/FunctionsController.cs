// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using ABCRetail.Web.Models;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Web.Controllers;

/// <summary>
/// Exercises the four Azure Functions that front the storage services, and shows what
/// each one returned.
/// </summary>
public class FunctionsController : Controller
{
    private readonly IStorageFunctionsClient _functions;

    public FunctionsController(IStorageFunctionsClient functions)
    {
        _functions = functions;
    }

    public IActionResult Index()
    {
        return View(new FunctionsViewModel
        {
            IsConfigured = _functions.IsConfigured,
            BaseUrl = _functions.BaseUrl
        });
    }

    /// <summary>Calls all four functions in turn and reports each result.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
    {
        var stamp = DateTimeOffset.UtcNow;
        var reference = $"RUN-{stamp:yyyyMMddHHmmss}";
        var results = new List<FunctionCallResult>();

        results.Add(await _functions.StoreTableEntityAsync(
            "FunctionAudit",
            "TASK3",
            reference,
            new Dictionary<string, string>
            {
                ["Source"] = "ABC Retail web application",
                ["Operation"] = "Store information into Azure Tables",
                ["RunAtUtc"] = stamp.ToString("u")
            },
            cancellationToken));

        var note = System.Text.Encoding.UTF8.GetBytes(
            $"Written by the UploadBlob function on behalf of the web application at {stamp:u}.");

        results.Add(await _functions.UploadBlobAsync(
            "product-images", $"{reference}.txt", "text/plain", note, cancellationToken));

        results.Add(await _functions.QueueTransactionAsync(
            "order-processing",
            $"Transaction {reference} recorded by the web application",
            reference,
            cancellationToken));

        // The queue is read as well as written, because the brief asks for a queue that is
        // written to and from rather than only produced into.
        results.Add(await _functions.ReadQueueAsync("order-processing", cancellationToken));

        results.Add(await _functions.WriteFileAsync(
            "logs",
            $"functions-{stamp:yyyy-MM-dd}.log",
            $"All four storage functions were invoked by the web application, run reference {reference}",
            cancellationToken));

        return View(nameof(Index), new FunctionsViewModel
        {
            IsConfigured = _functions.IsConfigured,
            BaseUrl = _functions.BaseUrl,
            Results = results
        });
    }
}
