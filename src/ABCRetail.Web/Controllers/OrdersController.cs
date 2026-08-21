// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using ABCRetail.Web.Models;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ABCRetail.Web.Controllers;

/// <summary>
/// Places customer orders. An order is written to the Orders table and its work item is
/// put on the order-processing queue; nothing here completes the order, because that only
/// happens once the message is taken off the queue by the processing screen.
/// </summary>
public class OrdersController : Controller
{
    private readonly ITableStorageService<Order> _orders;
    private readonly ITableStorageService<CustomerProfile> _customers;
    private readonly ITableStorageService<Product> _products;
    private readonly IActivityRecorder _activity;

    public OrdersController(
        ITableStorageService<Order> orders,
        ITableStorageService<CustomerProfile> customers,
        ITableStorageService<Product> products,
        IActivityRecorder activity)
    {
        _orders = orders;
        _customers = customers;
        _products = products;
        _activity = activity;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var orders = await _orders.GetAllAsync(Order.Partition, cancellationToken);
        return View(orders.OrderByDescending(o => o.PlacedOn).ToList());
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateChoicesAsync(cancellationToken);
        return View(new Order());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Order order, CancellationToken cancellationToken)
    {
        // ProductKey arrives as "category|rowKey" because a product's identity spans both,
        // and a select element can only carry one value.
        var productParts = (order.ProductKey ?? string.Empty).Split('|', 2);
        Product? product = productParts.Length == 2
            ? await _products.GetAsync(productParts[0], productParts[1], cancellationToken)
            : null;

        var customer = string.IsNullOrWhiteSpace(order.CustomerRowKey)
            ? null
            : await _customers.GetAsync(CustomerProfile.Partition, order.CustomerRowKey, cancellationToken);

        if (product is null)
        {
            ModelState.AddModelError(nameof(order.ProductKey), "Select a product from the catalogue.");
        }
        else if (product.StockLevel < order.Quantity)
        {
            ModelState.AddModelError(nameof(order.Quantity),
                $"Only {product.StockLevel} units of {product.ProductName} are in stock.");
        }

        if (customer is null)
        {
            ModelState.AddModelError(nameof(order.CustomerRowKey), "Select a registered customer.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateChoicesAsync(cancellationToken);
            return View(order);
        }

        order.PartitionKey = Order.Partition;
        order.RowKey = Guid.NewGuid().ToString();
        order.OrderNumber = $"ORD-{DateTimeOffset.UtcNow:yyyyMMdd}-{order.RowKey[..4].ToUpperInvariant()}";
        order.CustomerName = customer!.FullName;
        order.ProductCategory = product!.PartitionKey;
        order.ProductName = product.ProductName;
        order.UnitPrice = product.Price;
        order.TotalPrice = product.Price * order.Quantity;
        order.Status = OrderStatus.Submitted;
        order.PlacedOn = DateTimeOffset.UtcNow;

        await _orders.AddAsync(order, cancellationToken);

        // The reference carries the row key rather than the order number, so the consumer
        // can fetch the entity directly instead of scanning the table for a match.
        await _activity.RecordAsync(
            QueueNames.OrderProcessing,
            "OrderPlaced",
            $"Processing order {order.OrderNumber} for {order.CustomerName}, " +
            $"{order.Quantity} x {order.ProductName}",
            order.RowKey,
            cancellationToken);

        TempData["Message"] =
            $"{order.OrderNumber} was written to the Orders table and queued on order-processing.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var order = await _orders.GetAsync(Order.Partition, id, cancellationToken);
        return order is null ? NotFound() : View(order);
    }

    private async Task PopulateChoicesAsync(CancellationToken cancellationToken)
    {
        var customers = await _customers.GetAllAsync(CustomerProfile.Partition, cancellationToken);
        var products = await _products.GetAllAsync(cancellationToken: cancellationToken);

        ViewBag.Customers = customers
            .OrderBy(c => c.Surname)
            .Select(c => new SelectListItem($"{c.FullName} ({c.City})", c.RowKey))
            .ToList();

        ViewBag.Products = products
            .OrderBy(p => p.ProductName)
            .Select(p => new SelectListItem(
                $"{p.ProductName} - {Money.Format(p.Price)} ({p.StockLevel} in stock)",
                $"{p.PartitionKey}|{p.RowKey}",
                false,
                p.StockLevel == 0))
            .ToList();
    }
}
