// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using ABCRetail.Web.Models;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Web.Controllers;

/// <summary>
/// Monitors the two Azure queues and acts as the consumer for them. Taking a message off
/// a queue is what advances an order, so this screen is part of the workflow rather than
/// a read-only view of it.
/// </summary>
public class QueuesController : Controller
{
    private readonly IQueueStorageService _queues;
    private readonly ITableStorageService<Order> _orders;
    private readonly ITableStorageService<Product> _products;
    private readonly IActivityRecorder _activity;

    public QueuesController(
        IQueueStorageService queues,
        ITableStorageService<Order> orders,
        ITableStorageService<Product> products,
        IActivityRecorder activity)
    {
        _queues = queues;
        _orders = orders;
        _products = products;
        _activity = activity;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Each queue is independent, so the depth and peek calls for all of them are issued
        // together rather than one queue after another.
        var snapshots = await Task.WhenAll(QueueNames.All.Select(async name =>
        {
            var depthTask = _queues.GetDepthAsync(name, cancellationToken);
            var messagesTask = _queues.PeekAsync(name, 20, cancellationToken);
            await Task.WhenAll(depthTask, messagesTask);

            return new QueueSnapshot
            {
                Name = name,
                Description = QueueNames.Describe(name),
                ApproximateCount = await depthTask,
                Messages = await messagesTask
            };
        }));

        return View(new QueueMonitorViewModel { Queues = snapshots });
    }

    /// <summary>Takes the next message off a queue and carries out the work it describes.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Process(string queueName, CancellationToken cancellationToken)
    {
        if (!QueueNames.All.Contains(queueName))
        {
            return BadRequest();
        }

        var message = await _queues.DequeueAsync(queueName, cancellationToken);

        if (message is null)
        {
            TempData["Message"] = $"The {queueName} queue is empty, so there was nothing to process.";
            return RedirectToAction(nameof(Index));
        }

        var outcome = message.Operation.Kind switch
        {
            "OrderPlaced" => await CompleteOrderAsync(message.Operation, cancellationToken),
            _ => $"Handled {message.Operation.Kind}: {message.Operation.Message}"
        };

        await _activity.LogAsync("INFO", $"{outcome} [dequeued from {queueName}]", cancellationToken);

        TempData["Message"] = outcome;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(string queueName, CancellationToken cancellationToken)
    {
        if (!QueueNames.All.Contains(queueName))
        {
            return BadRequest();
        }

        await _queues.ClearAsync(queueName, cancellationToken);
        await _activity.LogAsync("WARN", $"All messages were cleared from {queueName}", cancellationToken);

        TempData["Message"] = $"Every message was removed from {queueName}.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Applies an order message: the order is completed and the stock it consumed is taken
    /// off the product. This is the point at which the order actually takes effect.
    /// </summary>
    private async Task<string> CompleteOrderAsync(QueueOperation operation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(operation.Reference))
        {
            return "An order message arrived with no order reference and was discarded.";
        }

        var order = await _orders.GetAsync(Order.Partition, operation.Reference, cancellationToken);

        if (order is null)
        {
            return "The order named by the message no longer exists, so nothing was changed.";
        }

        if (order.IsComplete)
        {
            return $"{order.OrderNumber} had already been processed, so the message was discarded.";
        }

        order.Status = OrderStatus.Completed;
        order.ProcessedOn = DateTimeOffset.UtcNow;
        await _orders.UpdateAsync(order, cancellationToken);

        var product = await _products.GetAsync(order.ProductCategory, order.ProductRowKey, cancellationToken);
        var stockNote = string.Empty;

        if (product is not null)
        {
            var before = product.StockLevel;
            product.StockLevel = Math.Max(0, product.StockLevel - order.Quantity);
            await _products.UpdateAsync(product, cancellationToken);

            stockNote = $" Stock for {product.ProductName} moved from {before} to {product.StockLevel}.";

            await _activity.RecordAsync(
                QueueNames.InventoryManagement,
                "StockAdjusted",
                $"Stock adjusted for {product.ProductName}: {before} to {product.StockLevel} " +
                $"after {order.OrderNumber}",
                product.RowKey,
                cancellationToken);
        }

        return $"{order.OrderNumber} was processed and marked complete.{stockNote}";
    }
}
