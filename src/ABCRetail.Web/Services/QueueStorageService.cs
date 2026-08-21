// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using ABCRetail.Web.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace ABCRetail.Web.Services;

/// <summary>
/// Azure Queue Storage implementation of <see cref="IQueueStorageService"/>.
/// </summary>
/// <remarks>
/// One <see cref="QueueClient"/> is held per queue name for the lifetime of the
/// application. The clients are thread safe and pool their connections, so building them
/// once avoids the socket churn of constructing a client per request.
/// </remarks>
public class QueueStorageService : IQueueStorageService
{
    private readonly string _connectionString;
    private readonly ILogger<QueueStorageService> _logger;
    private readonly Dictionary<string, QueueClient> _clients = [];
    private readonly HashSet<string> _created = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public QueueStorageService(string connectionString, ILogger<QueueStorageService> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task SendAsync(string queueName, QueueOperation operation, CancellationToken cancellationToken = default)
    {
        var queue = await ResolveAsync(queueName, cancellationToken);
        await queue.SendMessageAsync(operation.ToJson(), cancellationToken);
        _logger.LogInformation("Queued {Kind} on {Queue}: {Message}", operation.Kind, queueName, operation.Message);
    }

    public async Task<IReadOnlyList<QueuedMessage>> PeekAsync(string queueName, int maxMessages = 20, CancellationToken cancellationToken = default)
    {
        var queue = await ResolveAsync(queueName, cancellationToken);

        // Peek rather than receive, so simply opening the monitoring screen does not make
        // messages invisible to the code that is supposed to process them.
        var peeked = await queue.PeekMessagesAsync(Math.Clamp(maxMessages, 1, 32), cancellationToken);

        return peeked.Value
            .Select(m => new QueuedMessage(
                m.MessageId,
                QueueOperation.FromJson(m.Body.ToString()),
                m.InsertedOn,
                m.DequeueCount,
                m.Body.ToString()))
            .ToList();
    }

    public async Task<QueuedMessage?> DequeueAsync(string queueName, CancellationToken cancellationToken = default)
    {
        var queue = await ResolveAsync(queueName, cancellationToken);
        var received = await queue.ReceiveMessagesAsync(1, cancellationToken: cancellationToken);
        var message = received.Value.FirstOrDefault();

        if (message is null)
        {
            return null;
        }

        // The message is deleted only after it has been read successfully. Had the caller
        // failed instead, the visibility timeout would return it to the queue for a retry,
        // which is the at-least-once behaviour Queue Storage is built around.
        await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);

        return new QueuedMessage(
            message.MessageId,
            QueueOperation.FromJson(message.Body.ToString()),
            message.InsertedOn,
            message.DequeueCount,
            message.Body.ToString());
    }

    public async Task<int> GetDepthAsync(string queueName, CancellationToken cancellationToken = default)
    {
        var queue = await ResolveAsync(queueName, cancellationToken);
        QueueProperties properties = await queue.GetPropertiesAsync(cancellationToken);
        return properties.ApproximateMessagesCount;
    }

    public async Task ClearAsync(string queueName, CancellationToken cancellationToken = default)
    {
        var queue = await ResolveAsync(queueName, cancellationToken);
        await queue.ClearMessagesAsync(cancellationToken);
    }

    private async Task<QueueClient> ResolveAsync(string queueName, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_clients.TryGetValue(queueName, out var client))
            {
                client = new QueueClient(_connectionString, queueName);
                _clients[queueName] = client;
            }

            if (_created.Add(queueName))
            {
                await client.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }

            return client;
        }
        finally
        {
            _gate.Release();
        }
    }
}
