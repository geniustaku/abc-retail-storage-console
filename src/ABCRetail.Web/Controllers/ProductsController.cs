// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using ABCRetail.Web.Models;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Web.Controllers;

/// <summary>
/// Maintains the product catalogue. Each product spans both storage services: the
/// record itself goes to the Products table, while its image goes to Blob Storage and
/// is referenced from the record by URL.
/// </summary>
public class ProductsController : Controller
{
    private readonly ITableStorageService<Product> _products;
    private readonly IBlobStorageService _blobs;
    private readonly IActivityRecorder _activity;

    public ProductsController(
        ITableStorageService<Product> products,
        IBlobStorageService blobs,
        IActivityRecorder activity)
    {
        _products = products;
        _blobs = blobs;
        _activity = activity;
    }

    public async Task<IActionResult> Index(string? category, CancellationToken cancellationToken)
    {
        // Filtering by category is a single-partition query, which is the whole reason
        // category was chosen as the partition key.
        var products = await _products.GetAllAsync(
            string.IsNullOrWhiteSpace(category) ? null : category,
            cancellationToken);

        ViewData["Category"] = category;
        return View(products.OrderBy(p => p.ProductName).ToList());
    }

    public async Task<IActionResult> Details(string category, string id, CancellationToken cancellationToken)
    {
        var product = await _products.GetAsync(category, id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }

    public IActionResult Create() => View(new Product());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create(Product product, IFormFile? image, CancellationToken cancellationToken)
    {
        if (image is not null && image.Length > 0 && !_blobs.IsAcceptable(image, out var reason))
        {
            ModelState.AddModelError(nameof(image), reason!);
        }

        if (!ModelState.IsValid)
        {
            return View(product);
        }

        product.RowKey = Guid.NewGuid().ToString();
        product.CreatedOn = DateTimeOffset.UtcNow;

        // The blob is written first. If the upload fails the table row is never created,
        // which leaves the catalogue consistent rather than holding a product that points
        // at an image that does not exist.
        if (image is not null && image.Length > 0)
        {
            var upload = await _blobs.UploadAsync(image, cancellationToken);
            product.ImageBlobName = upload.BlobName;
            product.ImageUrl = upload.Url;

            await _activity.RecordAsync(
                QueueNames.InventoryManagement,
                "ImageUploaded",
                $"Uploading image \"{upload.BlobName}\" for {product.ProductName}",
                product.RowKey,
                cancellationToken);
        }

        await _products.AddAsync(product, cancellationToken);

        await _activity.RecordAsync(
            QueueNames.InventoryManagement,
            "ProductCreated",
            $"Inventory item created: {product.ProductName} in {product.PartitionKey}, " +
            $"{product.StockLevel} units on hand",
            product.RowKey,
            cancellationToken);

        TempData["Message"] = product.HasImage
            ? $"{product.ProductName} was written to the Products table and its image uploaded to Blob Storage."
            : $"{product.ProductName} was written to the Products table.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string category, string id, CancellationToken cancellationToken)
    {
        var product = await _products.GetAsync(category, id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Edit(
        Product product,
        string originalCategory,
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        if (image is not null && image.Length > 0 && !_blobs.IsAcceptable(image, out var reason))
        {
            ModelState.AddModelError(nameof(image), reason!);
        }

        if (!ModelState.IsValid)
        {
            return View(product);
        }

        var existing = await _products.GetAsync(originalCategory, product.RowKey, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        product.CreatedOn = existing.CreatedOn;
        product.ImageUrl = existing.ImageUrl;
        product.ImageBlobName = existing.ImageBlobName;

        if (image is not null && image.Length > 0)
        {
            var upload = await _blobs.UploadAsync(image, cancellationToken);
            await _blobs.DeleteAsync(existing.ImageBlobName, cancellationToken);

            product.ImageBlobName = upload.BlobName;
            product.ImageUrl = upload.Url;

            await _activity.RecordAsync(
                QueueNames.InventoryManagement,
                "ImageUploaded",
                $"Uploading image \"{upload.BlobName}\" for {product.ProductName}",
                product.RowKey,
                cancellationToken);
        }

        if (existing.StockLevel != product.StockLevel)
        {
            await _activity.RecordAsync(
                QueueNames.InventoryManagement,
                "StockAdjusted",
                $"Stock adjusted for {product.ProductName}: " +
                $"{existing.StockLevel} to {product.StockLevel}",
                product.RowKey,
                cancellationToken);
        }

        // The partition key is part of an entity's identity, so a category change is a
        // delete and an insert rather than an update in place.
        if (!string.Equals(originalCategory, product.PartitionKey, StringComparison.Ordinal))
        {
            await _products.AddAsync(product, cancellationToken);
            await _products.DeleteAsync(originalCategory, product.RowKey, cancellationToken);
        }
        else
        {
            await _products.UpdateAsync(product, cancellationToken);
        }

        TempData["Message"] = $"{product.ProductName} was updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(string category, string id, CancellationToken cancellationToken)
    {
        var product = await _products.GetAsync(category, id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string category, string id, CancellationToken cancellationToken)
    {
        var product = await _products.GetAsync(category, id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        // Blob first again, so a failure here does not orphan the image with no record
        // left in the table pointing at it.
        await _blobs.DeleteAsync(product.ImageBlobName, cancellationToken);
        await _products.DeleteAsync(category, id, cancellationToken);

        await _activity.RecordAsync(
            QueueNames.InventoryManagement,
            "ProductRemoved",
            $"Inventory item removed: {product.ProductName} from {category}",
            product.RowKey,
            cancellationToken);

        TempData["Message"] = $"{product.ProductName} was removed from the Products table and Blob Storage.";
        return RedirectToAction(nameof(Index));
    }
}
