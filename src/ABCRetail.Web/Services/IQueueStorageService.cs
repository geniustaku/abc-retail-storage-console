// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using ABCRetail.Web.Models;

namespace ABCRetail.Web.Services;

/// <summary>
/// Contract over Azure Queue Storage. Kept deliberately small: an application that
/// produces work and a screen that observes it need only these five operations.
/// </summary>
public interface IQueueStorageService
{
    /// <summary>Places one operation on the named queue.</summary>
    Task SendAsync(string queueName, QueueOperation operation, CancellationToken cancellationToken = default);

    /// <summary>Reads messages without removing them, for the monitoring screen.</summary>
    Task<IReadOnlyList<QueuedMessage>> PeekAsync(string queueName, int maxMessages = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes the next message off the queue and deletes it once handled. Returns null when
    /// the queue is empty.
    /// </summary>
    Task<QueuedMessage?> DequeueAsync(string queueName, CancellationToken cancellationToken = default);

    /// <summary>Approximate depth, which is what the Queue service reports.</summary>
    Task<int> GetDepthAsync(string queueName, CancellationToken cancellationToken = default);

    Task ClearAsync(string queueName, CancellationToken cancellationToken = default);
}
