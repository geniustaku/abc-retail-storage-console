// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using ABCRetail.Web.Models;

namespace ABCRetail.Web.Services;

/// <summary>
/// Records a business event to both Azure Queue Storage and the log file on Azure Files.
/// </summary>
/// <remarks>
/// Controllers call this rather than the queue and file services directly. Keeping the
/// pairing in one place means no caller can queue work and forget to log it, and the two
/// storage concerns stay out of the controllers entirely.
/// </remarks>
public interface IActivityRecorder
{
    /// <summary>Queues an operation for a consumer to act on, and logs that it was queued.</summary>
    Task<QueueOperation> RecordAsync(
        string queueName,
        string kind,
        string message,
        string? reference = null,
        CancellationToken cancellationToken = default);

    /// <summary>Writes a log line without queuing work, for events with no consumer.</summary>
    Task LogAsync(string level, string message, CancellationToken cancellationToken = default);
}
