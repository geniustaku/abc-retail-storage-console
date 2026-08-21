// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using System.Text;
using ABCRetail.Web.Models;
using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;

namespace ABCRetail.Web.Services;

/// <summary>
/// Stores application log files and generated reports on an Azure file share.
/// </summary>
/// <remarks>
/// Azure Files has no append operation of its own, unlike an append blob. Adding a line
/// therefore means reading the current length, growing the file by the size of the new
/// bytes, and writing those bytes into the range that growth created. That sequence is
/// not atomic, so a semaphore serialises writers within this instance. Across several
/// App Service instances the correct answer would be a lease on the file; on the single
/// instance this application runs on, the semaphore is sufficient and honest.
/// </remarks>
public class FileShareService : IFileShareService
{
    private readonly ShareClient _share;
    private readonly ILogger<FileShareService> _logger;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private bool _initialised;

    public FileShareService(string connectionString, string shareName, ILogger<FileShareService> logger)
    {
        _share = new ShareClient(connectionString, shareName);
        ShareName = shareName;
        _logger = logger;
    }

    public string ShareName { get; }

    public async Task AppendLogAsync(string level, string message, CancellationToken cancellationToken = default)
    {
        var stamp = DateTimeOffset.UtcNow;
        var line = $"{stamp:yyyy-MM-dd HH:mm:ss} [{level.ToUpperInvariant()}] {message}{Environment.NewLine}";
        var fileName = $"app-{stamp:yyyy-MM-dd}.log";

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            var directory = await ResolveDirectoryAsync(IFileShareService.LogsDirectory, cancellationToken);
            var file = directory.GetFileClient(fileName);
            var bytes = Encoding.UTF8.GetBytes(line);

            long offset;
            if (await file.ExistsAsync(cancellationToken))
            {
                ShareFileProperties properties = await file.GetPropertiesAsync(cancellationToken: cancellationToken);
                offset = properties.ContentLength;
                await file.SetHttpHeadersAsync(
                    new ShareFileSetHttpHeadersOptions { NewSize = offset + bytes.Length },
                    cancellationToken: cancellationToken);
            }
            else
            {
                offset = 0;
                await file.CreateAsync(bytes.Length, cancellationToken: cancellationToken);
            }

            using var stream = new MemoryStream(bytes);
            await file.UploadRangeAsync(new HttpRange(offset, bytes.Length), stream, cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex)
        {
            // Logging must never take the request down with it. The failure is reported to
            // the platform log and the user's action is allowed to complete.
            _logger.LogError(ex, "Could not append to the log file on the {Share} share", ShareName);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task WriteFileAsync(string directory, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            var dir = await ResolveDirectoryAsync(directory, cancellationToken);
            var file = dir.GetFileClient(fileName);

            // Create replaces any existing file and sets its length in one call, which is
            // the closest Azure Files offers to an overwrite.
            await file.CreateAsync(content.Length, cancellationToken: cancellationToken);

            if (content.Length > 0)
            {
                using var stream = new MemoryStream(content);
                await file.UploadRangeAsync(new HttpRange(0, content.Length), stream, cancellationToken: cancellationToken);
            }

            _logger.LogInformation("Wrote {File} ({Bytes} bytes) to {Directory}", fileName, content.Length, directory);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<IReadOnlyList<ShareFileEntry>> ListAsync(string directory, CancellationToken cancellationToken = default)
    {
        var dir = await ResolveDirectoryAsync(directory, cancellationToken);
        var entries = new List<ShareFileEntry>();

        await foreach (ShareFileItem item in dir.GetFilesAndDirectoriesAsync(cancellationToken: cancellationToken))
        {
            if (item.IsDirectory)
            {
                continue;
            }

            entries.Add(new ShareFileEntry(
                item.Name,
                directory,
                item.FileSize ?? 0,
                item.Properties?.LastModified));
        }

        return entries.OrderByDescending(e => e.Name).ToList();
    }

    public async Task<byte[]?> ReadAsync(string directory, string fileName, CancellationToken cancellationToken = default)
    {
        var dir = await ResolveDirectoryAsync(directory, cancellationToken);
        var file = dir.GetFileClient(fileName);

        try
        {
            var download = await file.DownloadAsync(cancellationToken: cancellationToken);
            using var buffer = new MemoryStream();
            await download.Value.Content.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<long> TotalSizeAsync(CancellationToken cancellationToken = default)
    {
        var logs = await ListAsync(IFileShareService.LogsDirectory, cancellationToken);
        var exports = await ListAsync(IFileShareService.ExportsDirectory, cancellationToken);
        return logs.Sum(f => f.SizeBytes) + exports.Sum(f => f.SizeBytes);
    }

    private async Task<ShareDirectoryClient> ResolveDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        if (!_initialised)
        {
            await _share.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _initialised = true;
        }

        var dir = _share.GetDirectoryClient(directory);
        await dir.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return dir;
    }
}
