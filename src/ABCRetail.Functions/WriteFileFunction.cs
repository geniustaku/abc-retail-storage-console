// ABC Retail - Azure Functions
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using System.Net;
using System.Text;
using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ABCRetail.Functions;

/// <summary>
/// Function 4 of 4. Appends a line to a log file on the Azure Files share.
/// </summary>
/// <remarks>
/// Azure Files has no append operation, unlike an append blob. A line is added by reading
/// the file's current length, growing the file by the length of the new bytes, and writing
/// those bytes into the range that growth created.
/// </remarks>
public class WriteFileFunction
{
    private const string ShareName = "application-logs";

    private readonly ILogger<WriteFileFunction> _logger;

    public WriteFileFunction(ILogger<WriteFileFunction> logger)
    {
        _logger = logger;
    }

    [Function("WriteFile")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "files")] HttpRequestData request)
    {
        var payload = await Json.ReadAsync<FileRequest>(request);

        if (payload is null || string.IsNullOrWhiteSpace(payload.Line))
        {
            return await Json.WriteAsync(request, HttpStatusCode.BadRequest,
                new { error = "A line of text is required." });
        }

        var directoryName = string.IsNullOrWhiteSpace(payload.Directory) ? "logs" : payload.Directory;
        var fileName = string.IsNullOrWhiteSpace(payload.FileName)
            ? $"functions-{DateTimeOffset.UtcNow:yyyy-MM-dd}.log"
            : payload.FileName;

        var share = new ShareClient(StorageConnection.Resolve(), ShareName);
        await share.CreateIfNotExistsAsync();

        var directory = share.GetDirectoryClient(directoryName);
        await directory.CreateIfNotExistsAsync();

        var file = directory.GetFileClient(fileName);
        var text = $"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} [FUNCTION] {payload.Line}{Environment.NewLine}";
        var bytes = Encoding.UTF8.GetBytes(text);

        long offset;
        if (await file.ExistsAsync())
        {
            ShareFileProperties properties = await file.GetPropertiesAsync();
            offset = properties.ContentLength;
            await file.SetHttpHeadersAsync(new ShareFileSetHttpHeadersOptions { NewSize = offset + bytes.Length });
        }
        else
        {
            offset = 0;
            await file.CreateAsync(bytes.Length);
        }

        using var stream = new MemoryStream(bytes);
        await file.UploadRangeAsync(new HttpRange(offset, bytes.Length), stream);

        _logger.LogInformation("Appended {Bytes} bytes to {Directory}/{File}", bytes.Length, directoryName, fileName);

        return await Json.WriteAsync(request, HttpStatusCode.OK, new
        {
            status = "appended",
            service = "Azure Files",
            share = ShareName,
            path = $"{directoryName}/{fileName}",
            bytesWritten = bytes.Length,
            fileSizeBytes = offset + bytes.Length,
            writtenAtUtc = DateTimeOffset.UtcNow
        });
    }
}
