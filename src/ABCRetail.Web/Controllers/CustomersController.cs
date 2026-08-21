// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using ABCRetail.Web.Models;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Web.Controllers;

/// <summary>
/// Maintains customer profiles in the CustomerProfiles table in Azure Table Storage.
/// </summary>
public class CustomersController : Controller
{
    private readonly ITableStorageService<CustomerProfile> _customers;
    private readonly IActivityRecorder _activity;

    public CustomersController(ITableStorageService<CustomerProfile> customers, IActivityRecorder activity)
    {
        _customers = customers;
        _activity = activity;
    }

    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        var customers = await _customers.GetAllAsync(CustomerProfile.Partition, cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Table Storage has no contains operator, so the filter is applied after the
            // read. Acceptable while the table sits in one partition; a larger customer
            // base would want a search index alongside the table.
            customers = customers
                .Where(c => c.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
                         || c.Email.Contains(search, StringComparison.OrdinalIgnoreCase)
                         || c.City.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        ViewData["Search"] = search;
        return View(customers.OrderBy(c => c.Surname).ThenBy(c => c.FirstName).ToList());
    }

    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetAsync(CustomerProfile.Partition, id, cancellationToken);
        return customer is null ? NotFound() : View(customer);
    }

    public IActionResult Create() => View(new CustomerProfile());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerProfile customer, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        customer.PartitionKey = CustomerProfile.Partition;
        customer.RowKey = Guid.NewGuid().ToString();
        customer.RegisteredOn = DateTimeOffset.UtcNow;

        await _customers.AddAsync(customer, cancellationToken);

        // Registering a customer creates no work for a consumer, so it is logged to the file
        // share without a queue message. Only things something must act on are queued.
        await _activity.LogAsync("INFO",
            $"Customer profile created: {customer.FullName} ({customer.Email})", cancellationToken);

        TempData["Message"] = $"{customer.FullName} was written to the CustomerProfiles table.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetAsync(CustomerProfile.Partition, id, cancellationToken);
        return customer is null ? NotFound() : View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CustomerProfile customer, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        var existing = await _customers.GetAsync(CustomerProfile.Partition, customer.RowKey, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        // The registration date is not on the form, so it is carried over from the stored
        // entity. A replace would otherwise reset it to the default.
        customer.PartitionKey = CustomerProfile.Partition;
        customer.RegisteredOn = existing.RegisteredOn;

        await _customers.UpdateAsync(customer, cancellationToken);

        TempData["Message"] = $"{customer.FullName} was updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetAsync(CustomerProfile.Partition, id, cancellationToken);
        return customer is null ? NotFound() : View(customer);
    }

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id, CancellationToken cancellationToken)
    {
        await _customers.DeleteAsync(CustomerProfile.Partition, id, cancellationToken);

        await _activity.LogAsync("WARN", $"Customer profile deleted: row key {id}", cancellationToken);

        TempData["Message"] = "Customer profile removed from the CustomerProfiles table.";
        return RedirectToAction(nameof(Index));
    }
}
