// ABC Retail - Azure Functions
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using System.Net;
using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ABCRetail.Functions;

/// <summary>
/// Function 3 of 4. Writes transaction messages to a queue and reads them back off it.
/// </summary>
/// <remarks>
/// Both directions live in one function because the brief treats the queue as a single
/// concern, and because a producer and a consumer that disagree about the message format
/// is a common source of defects. Keeping them together makes the contract obvious.
/// </remarks>
public class QueueTransactionFunction
{
    private readonly ILogger<QueueTransactionFunction> _logger;

    public QueueTransactionFunction(ILogger<QueueTransactionFunction> logger)
    {
        _logger = logger;
    }

    [Function("QueueTransaction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", "get", Route = "queue")] HttpRequestData request)
    {
        return request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            ? await WriteAsync(request)
            : await ReadAsync(request);
    }

    private async Task<HttpResponseData> WriteAsync(HttpRequestData request)
    {
        var payload = await Json.ReadAsync<QueueRequest>(request);

        if (payload is null || string.IsNullOrWhiteSpace(payload.Message))
        {
            return await Json.WriteAsync(request, HttpStatusCode.BadRequest,
                new { error = "A message is required." });
        }

        var queue = await ResolveAsync(payload.Queue);

        // The body carries JSON rather than bare text so a consumer gets the reference it
        // needs to act on the message without a second lookup.
        var body = JsonSerializer.Serialize(new
        {
            kind = "Transaction",
            message = payload.Message,
            reference = payload.Reference,
            queuedAtUtc = DateTimeOffset.UtcNow
        });

        var receipt = await queue.SendMessageAsync(body);
        var properties = await queue.GetPropertiesAsync();

        _logger.LogInformation("Queued a transaction on {Queue}", queue.Name);

        return await Json.WriteAsync(request, HttpStatusCode.OK, new
        {
            status = "queued",
            service = "Azure Queue Storage",
            direction = "write",
            queue = queue.Name,
            messageId = receipt.Value.MessageId,
            approximateDepth = properties.Value.ApproximateMessagesCount,
            queuedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private async Task<HttpResponseData> ReadAsync(HttpRequestData request)
    {
        var parameters = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var queue = await ResolveAsync(parameters["queue"]);
        var remove = string.Equals(parameters["mode"], "dequeue", StringComparison.OrdinalIgnoreCase);

        if (!remove)
        {
            // Peeking leaves the message visible to the real consumer, so reading the queue
            // for monitoring does not interfere with the work being processed.
            var peeked = await queue.PeekMessagesAsync(10);
            var properties = await queue.GetPropertiesAsync();

            return await Json.WriteAsync(request, HttpStatusCode.OK, new
            {
                status = "read",
                service = "Azure Queue Storage",
                direction = "read (peek)",
                queue = queue.Name,
                approximateDepth = properties.Value.ApproximateMessagesCount,
                messages = peeked.Value.Select(m => new { m.MessageId, body = m.Body.ToString(), m.InsertedOn })
            });
        }

        var received = await queue.ReceiveMessagesAsync(1);
        var message = received.Value.FirstOrDefault();

        if (message is null)
        {
            return await Json.WriteAsync(request, HttpStatusCode.OK, new
            {
                status = "empty",
                service = "Azure Queue Storage",
                direction = "read (dequeue)",
                queue = queue.Name
            });
        }

        // Deleted only after it has been read successfully. A failure before this point
        // returns the message to the queue for another attempt.
        await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt);

        return await Json.WriteAsync(request, HttpStatusCode.OK, new
        {
            status = "dequeued",
            service = "Azure Queue Storage",
            direction = "read (dequeue)",
            queue = queue.Name,
            messageId = message.MessageId,
            body = message.Body.ToString(),
            dequeueCount = message.DequeueCount
        });
    }

    private static async Task<QueueClient> ResolveAsync(string? name)
    {
        var queue = new QueueClient(
            StorageConnection.Resolve(),
            string.IsNullOrWhiteSpace(name) ? "order-processing" : name);

        await queue.CreateIfNotExistsAsync();
        return queue;
    }
}
