// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using Azure.Data.Tables;

namespace ABCRetail.Web.Services;

/// <summary>
/// Persistence contract over a single Azure Table. Kept generic so that each entity
/// type gets its own strongly typed client without a service class per table.
/// </summary>
public interface ITableStorageService<T> where T : class, ITableEntity, new()
{
    /// <summary>Reads every entity, or only one partition when a key is supplied.</summary>
    Task<IReadOnlyList<T>> GetAllAsync(string? partitionKey = null, CancellationToken cancellationToken = default);

    Task<T?> GetAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default);

    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
