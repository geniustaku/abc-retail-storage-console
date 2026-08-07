// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

namespace ABCRetail.Web.Services;

/// <summary>Outcome of a successful upload to Blob Storage.</summary>
/// <param name="BlobName">Generated name of the blob within the container.</param>
/// <param name="Url">Absolute URL the browser uses to fetch the image.</param>
public record BlobUploadResult(string BlobName, string Url);

/// <summary>
/// Contract for storing product imagery in Azure Blob Storage, kept separate from the
/// table services so the two storage concerns can evolve independently.
/// </summary>
public interface IBlobStorageService
{
    Task<BlobUploadResult> UploadAsync(IFormFile file, CancellationToken cancellationToken = default);

    Task DeleteAsync(string? blobName, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>Rejects a file before it reaches the network, returning why if invalid.</summary>
    bool IsAcceptable(IFormFile? file, out string? reason);
}
