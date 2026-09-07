// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

namespace ABCRetail.Web.Services;

/// <summary>One call to an Azure Function, with everything the page needs to report it.</summary>
public record FunctionCallResult(
    string Name,
    string Service,
    string Endpoint,
    bool Success,
    int StatusCode,
    string ResponseBody,
    long ElapsedMilliseconds,
    string? Error = null);

/// <summary>
/// Calls the four Azure Functions that front the storage services.
/// </summary>
/// <remarks>
/// The web application no longer has to reach the storage account itself for these four
/// operations. It posts to a function, and the function does the storage work. That is
/// what moves the load off the web tier: the functions scale on their own and are billed
/// per execution rather than sitting idle inside the App Service.
/// </remarks>
public interface IStorageFunctionsClient
{
    /// <summary>False when no function base URL or key has been configured.</summary>
    bool IsConfigured { get; }

    string BaseUrl { get; }

    Task<FunctionCallResult> StoreTableEntityAsync(
        string table, string partitionKey, string rowKey,
        IDictionary<string, string> properties, CancellationToken cancellationToken = default);

    Task<FunctionCallResult> UploadBlobAsync(
        string container, string fileName, string contentType,
        byte[] content, CancellationToken cancellationToken = default);

    Task<FunctionCallResult> QueueTransactionAsync(
        string queue, string message, string? reference, CancellationToken cancellationToken = default);

    Task<FunctionCallResult> ReadQueueAsync(string queue, CancellationToken cancellationToken = default);

    Task<FunctionCallResult> WriteFileAsync(
        string directory, string fileName, string line, CancellationToken cancellationToken = default);
}
