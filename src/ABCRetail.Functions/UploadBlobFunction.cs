// ABC Retail - Azure Functions
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using System.Net;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ABCRetail.Functions;

/// <summary>
/// Function 2 of 4. Writes a file to Azure Blob Storage and returns its URL.
/// </summary>
public class UploadBlobFunction
{
    private const int MaxBytes = 5 * 1024 * 1024;

    private readonly ILogger<UploadBlobFunction> _logger;

    public UploadBlobFunction(ILogger<UploadBlobFunction> logger)
    {
        _logger = logger;
    }

    [Function("UploadBlob")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "blobs")] HttpRequestData request)
    {
        var payload = await Json.ReadAsync<BlobRequest>(request);

        if (payload is null || string.IsNullOrWhiteSpace(payload.ContentBase64))
        {
            return await Json.WriteAsync(request, HttpStatusCode.BadRequest,
                new { error = "File content is required, encoded as base64." });
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload.ContentBase64);
        }
        catch (FormatException)
        {
            return await Json.WriteAsync(request, HttpStatusCode.BadRequest,
                new { error = "The content supplied is not valid base64." });
        }

        if (bytes.Length > MaxBytes)
        {
            return await Json.WriteAsync(request, HttpStatusCode.RequestEntityTooLarge,
                new { error = $"Files must be {MaxBytes / 1024 / 1024} MB or smaller." });
        }

        var container = new BlobContainerClient(
            StorageConnection.Resolve(),
            string.IsNullOrWhiteSpace(payload.Container) ? "product-images" : payload.Container);

        await container.CreateIfNotExistsAsync(PublicAccessType.Blob);

        // The supplied file name is only used for its extension. The stored name is
        // generated so a caller cannot overwrite an existing blob or shape the blob path.
        var extension = Path.GetExtension(payload.FileName ?? string.Empty).ToLowerInvariant();
        var blobName = $"{Guid.NewGuid():N}{extension}";
        var blob = container.GetBlobClient(blobName);

        using var stream = new MemoryStream(bytes);
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = string.IsNullOrWhiteSpace(payload.ContentType) ? "application/octet-stream" : payload.ContentType,
                CacheControl = "public, max-age=31536000"
            }
        });

        _logger.LogInformation("Uploaded blob {BlobName} ({Bytes} bytes)", blobName, bytes.Length);

        return await Json.WriteAsync(request, HttpStatusCode.OK, new
        {
            status = "uploaded",
            service = "Azure Blob Storage",
            container = container.Name,
            blobName,
            url = blob.Uri.ToString(),
            sizeBytes = bytes.Length,
            writtenAtUtc = DateTimeOffset.UtcNow
        });
    }
}
