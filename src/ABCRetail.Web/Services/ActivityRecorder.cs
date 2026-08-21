// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using ABCRetail.Web.Models;

namespace ABCRetail.Web.Services;

/// <inheritdoc cref="IActivityRecorder"/>
public class ActivityRecorder : IActivityRecorder
{
    private readonly IQueueStorageService _queues;
    private readonly IFileShareService _files;

    public ActivityRecorder(IQueueStorageService queues, IFileShareService files)
    {
        _queues = queues;
        _files = files;
    }

    public async Task<QueueOperation> RecordAsync(
        string queueName,
        string kind,
        string message,
        string? reference = null,
        CancellationToken cancellationToken = default)
    {
        var operation = new QueueOperation
        {
            Kind = kind,
            Message = message,
            Reference = reference,
            QueuedAt = DateTimeOffset.UtcNow
        };

        // The queue write comes first and is allowed to throw. Work that never reached the
        // queue must not be reported as queued in the log, and the caller needs to know.
        await _queues.SendAsync(queueName, operation, cancellationToken);

        await _files.AppendLogAsync("INFO", $"{message} [queued on {queueName}]", cancellationToken);

        return operation;
    }

    public Task LogAsync(string level, string message, CancellationToken cancellationToken = default)
        => _files.AppendLogAsync(level, message, cancellationToken);
}
