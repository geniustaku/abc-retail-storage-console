// ABC Retail - Azure Functions
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using System.Net;
using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ABCRetail.Functions;

/// <summary>
/// Function 1 of 4. Writes an entity to Azure Table Storage.
/// </summary>
/// <remarks>
/// The table name arrives in the request rather than being fixed in the function, so the
/// same function serves CustomerProfiles, Products and Orders instead of needing one
/// deployment per table.
/// </remarks>
public class StoreTableEntityFunction
{
    private readonly ILogger<StoreTableEntityFunction> _logger;

    public StoreTableEntityFunction(ILogger<StoreTableEntityFunction> logger)
    {
        _logger = logger;
    }

    [Function("StoreTableEntity")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tables")] HttpRequestData request)
    {
        var payload = await Json.ReadAsync<TableEntityRequest>(request);

        if (payload is null || string.IsNullOrWhiteSpace(payload.Table))
        {
            return await Json.WriteAsync(request, HttpStatusCode.BadRequest,
                new { error = "A table name is required." });
        }

        var partitionKey = string.IsNullOrWhiteSpace(payload.PartitionKey) ? "DEFAULT" : payload.PartitionKey;
        var rowKey = string.IsNullOrWhiteSpace(payload.RowKey) ? Guid.NewGuid().ToString() : payload.RowKey;

        var table = new TableClient(StorageConnection.Resolve(), payload.Table);
        await table.CreateIfNotExistsAsync();

        var entity = new TableEntity(partitionKey, rowKey);
        foreach (var pair in payload.Properties ?? new Dictionary<string, string>())
        {
            entity[pair.Key] = pair.Value;
        }

        // Upsert rather than insert, so a repeated call from the web application updates
        // the entity instead of failing on a duplicate key.
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace);

        _logger.LogInformation("Stored entity {Partition}/{Row} in {Table}", partitionKey, rowKey, payload.Table);

        return await Json.WriteAsync(request, HttpStatusCode.OK, new
        {
            status = "stored",
            service = "Azure Table Storage",
            table = payload.Table,
            partitionKey,
            rowKey,
            propertyCount = payload.Properties?.Count ?? 0,
            writtenAtUtc = DateTimeOffset.UtcNow
        });
    }
}

/// <summary>Shared JSON handling so every function reads and replies the same way.</summary>
internal static class Json
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<T?> ReadAsync<T>(HttpRequestData request)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(body, Options);
    }

    public static async Task<HttpResponseData> WriteAsync(HttpRequestData request, HttpStatusCode status, object payload)
    {
        var response = request.CreateResponse(status);
        await response.WriteAsJsonAsync(payload);
        response.StatusCode = status;
        return response;
    }
}
