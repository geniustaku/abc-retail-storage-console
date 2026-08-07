// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using Azure;
using Azure.Data.Tables;

namespace ABCRetail.Web.Services;

/// <summary>
/// Azure Table Storage implementation of <see cref="ITableStorageService{T}"/>.
/// </summary>
/// <remarks>
/// Registered as a singleton per entity type. <see cref="TableClient"/> is thread safe
/// and pools its connections, so sharing one instance for the lifetime of the
/// application avoids the socket exhaustion that comes from constructing a client
/// per request.
/// </remarks>
public class TableStorageService<T> : ITableStorageService<T> where T : class, ITableEntity, new()
{
    private readonly TableClient _table;
    private readonly ILogger<TableStorageService<T>> _logger;
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private bool _initialised;

    public TableStorageService(string connectionString, string tableName, ILogger<TableStorageService<T>> logger)
    {
        _table = new TableClient(connectionString, tableName);
        _logger = logger;
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(string? partitionKey = null, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        var filter = partitionKey is null
            ? null
            : TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");

        var results = new List<T>();
        await foreach (var entity in _table.QueryAsync<T>(filter, cancellationToken: cancellationToken))
        {
            results.Add(entity);
        }

        return results;
    }

    public async Task<T?> GetAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        try
        {
            var response = await _table.GetEntityAsync<T>(partitionKey, rowKey, cancellationToken: cancellationToken);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _table.AddEntityAsync(entity, cancellationToken);
        _logger.LogInformation("Added {Entity} {PartitionKey}/{RowKey}", typeof(T).Name, entity.PartitionKey, entity.RowKey);
        return entity;
    }

    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        // Replace rather than merge so that cleared fields are actually cleared, and
        // unconditionally so an edit is not rejected by a stale ETag carried through
        // the round trip to the browser.
        await _table.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task DeleteAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await _table.DeleteEntityAsync(partitionKey, rowKey, ETag.All, cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        // Table Storage exposes no aggregate functions, so the rows are projected down
        // to their keys and counted client side. Cheap at catalogue scale, and it keeps
        // the payload off the wire.
        var count = 0;
        await foreach (var _ in _table.QueryAsync<TableEntity>(select: ["RowKey"], cancellationToken: cancellationToken))
        {
            count++;
        }

        return count;
    }

    private async Task EnsureTableAsync(CancellationToken cancellationToken)
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
                await _table.CreateIfNotExistsAsync(cancellationToken);
                _initialised = true;
            }
        }
        finally
        {
            _initGate.Release();
        }
    }
}
