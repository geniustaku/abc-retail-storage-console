// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ABCRetail.Web.Services;

namespace ABCRetail.Web.Services;

/// <inheritdoc cref="IStorageFunctionsClient"/>
public class StorageFunctionsClient : IStorageFunctionsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<StorageFunctionsClient> _logger;
    private readonly string _key;

    public StorageFunctionsClient(HttpClient http, IConfiguration configuration, ILogger<StorageFunctionsClient> logger)
    {
        _http = http;
        _logger = logger;

        BaseUrl = (configuration["Functions:BaseUrl"] ?? string.Empty).TrimEnd('/');
        _key = configuration["Functions:Key"] ?? string.Empty;

        // The functions take a while to wake from cold on the consumption plan, so the
        // timeout is deliberately generous rather than the default.
        _http.Timeout = TimeSpan.FromSeconds(100);
    }

    public string BaseUrl { get; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(_key);

    public Task<FunctionCallResult> StoreTableEntityAsync(
        string table, string partitionKey, string rowKey,
        IDictionary<string, string> properties, CancellationToken cancellationToken = default)
        => PostAsync("StoreTableEntity", "Azure Table Storage", "tables",
            new { table, partitionKey, rowKey, properties }, cancellationToken);

    public Task<FunctionCallResult> UploadBlobAsync(
        string container, string fileName, string contentType,
        byte[] content, CancellationToken cancellationToken = default)
        => PostAsync("UploadBlob", "Azure Blob Storage", "blobs",
            new { container, fileName, contentType, contentBase64 = Convert.ToBase64String(content) }, cancellationToken);

    public Task<FunctionCallResult> QueueTransactionAsync(
        string queue, string message, string? reference, CancellationToken cancellationToken = default)
        => PostAsync("QueueTransaction", "Azure Queue Storage", "queue",
            new { queue, message, reference }, cancellationToken);

    public Task<FunctionCallResult> WriteFileAsync(
        string directory, string fileName, string line, CancellationToken cancellationToken = default)
        => PostAsync("WriteFile", "Azure Files", "files",
            new { directory, fileName, line }, cancellationToken);

    public async Task<FunctionCallResult> ReadQueueAsync(string queue, CancellationToken cancellationToken = default)
    {
        var route = $"queue?queue={Uri.EscapeDataString(queue)}&mode=peek";
        return await SendAsync("QueueTransaction", "Azure Queue Storage", route, HttpMethod.Get, null, cancellationToken);
    }

    private Task<FunctionCallResult> PostAsync(
        string name, string service, string route, object payload, CancellationToken cancellationToken)
        => SendAsync(name, service, route, HttpMethod.Post, payload, cancellationToken);

    private async Task<FunctionCallResult> SendAsync(
        string name, string service, string route, HttpMethod method, object? payload, CancellationToken cancellationToken)
    {
        var separator = route.Contains('?') ? "&" : "?";
        var endpoint = $"{BaseUrl}/{route}";
        var requestUri = $"{endpoint}{separator}code={_key}";
        var watch = Stopwatch.StartNew();

        if (!IsConfigured)
        {
            return new FunctionCallResult(name, service, endpoint, false, 0, string.Empty, 0,
                "No function URL or key is configured for this environment.");
        }

        try
        {
            using var message = new HttpRequestMessage(method, requestUri);

            if (payload is not null)
            {
                message.Content = new StringContent(
                    JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            }

            using var response = await _http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            watch.Stop();

            return new FunctionCallResult(
                name, service, endpoint, response.IsSuccessStatusCode,
                (int)response.StatusCode, Prettify(body), watch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            watch.Stop();
            _logger.LogError(ex, "Call to the {Name} function failed", name);

            // A function being unreachable must not take a page down with it, so the
            // failure is returned as a result the view can display.
            return new FunctionCallResult(
                name, service, endpoint, false, 0, string.Empty, watch.ElapsedMilliseconds, ex.Message);
        }
    }

    /// <summary>Indents the JSON so the response is readable on screen.</summary>
    private static string Prettify(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
