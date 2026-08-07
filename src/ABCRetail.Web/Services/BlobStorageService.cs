// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ABCRetail.Web.Services;

/// <summary>
/// Stores product imagery in an Azure Blob Storage container.
/// </summary>
/// <remarks>
/// The container is created with public read access at the blob level, which lets the
/// catalogue views point an image tag straight at the blob URL. The alternative, issuing
/// a shared access signature per image or streaming the bytes back through a controller,
/// buys access control this catalogue does not need and costs a round trip per thumbnail.
/// </remarks>
public class BlobStorageService : IBlobStorageService
{
    private const long MaxBytes = 5 * 1024 * 1024;

    private static readonly string[] AllowedContentTypes =
    [
        "image/jpeg", "image/png", "image/webp", "image/gif"
    ];

    private static readonly string[] AllowedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    ];

    private readonly BlobContainerClient _container;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private bool _initialised;

    public BlobStorageService(string connectionString, string containerName, ILogger<BlobStorageService> logger)
    {
        _container = new BlobContainerClient(connectionString, containerName);
        _logger = logger;
    }

    public bool IsAcceptable(IFormFile? file, out string? reason)
    {
        if (file is null || file.Length == 0)
        {
            reason = "Choose an image to upload.";
            return false;
        }

        if (file.Length > MaxBytes)
        {
            reason = $"Images must be {MaxBytes / 1024 / 1024} MB or smaller.";
            return false;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        // Both the declared content type and the extension are checked, since either
        // one on its own is trivially spoofed by the client.
        if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase)
            || !AllowedExtensions.Contains(extension))
        {
            reason = "Only JPG, PNG, WEBP and GIF images are accepted.";
            return false;
        }

        reason = null;
        return true;
    }

    public async Task<BlobUploadResult> UploadAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);

        // The client supplied file name is discarded in favour of a generated one. It
        // removes any chance of a collision overwriting another product's image and
        // stops user input from reaching the blob path.
        var blobName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        var blob = _container.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = file.ContentType,
                    CacheControl = "public, max-age=31536000"
                }
            },
            cancellationToken);

        _logger.LogInformation("Uploaded blob {BlobName} ({Bytes} bytes)", blobName, file.Length);

        return new BlobUploadResult(blobName, blob.Uri.ToString());
    }

    public async Task DeleteAsync(string? blobName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return;
        }

        await EnsureContainerAsync(cancellationToken);
        await _container.DeleteBlobIfExistsAsync(blobName, cancellationToken: cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);

        var count = 0;
        await foreach (var _ in _container.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            count++;
        }

        return count;
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_initialised)
        {
            return;
        }

        await _initGate.WaitAsync(cancellationToken);
        try
        {
            if (!_initialised)
            {
                await _container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
                _initialised = true;
            }
        }
        finally
        {
            _initGate.Release();
        }
    }
}
