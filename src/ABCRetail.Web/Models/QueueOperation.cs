// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ABCRetail.Web.Models;

/// <summary>Names of the queues this application writes to.</summary>
public static class QueueNames
{
    public const string OrderProcessing = "order-processing";
    public const string InventoryManagement = "inventory-management";

    public static readonly string[] All = [OrderProcessing, InventoryManagement];

    public static string Describe(string queueName) => queueName switch
    {
        OrderProcessing => "Orders waiting to be picked, packed and dispatched",
        InventoryManagement => "Catalogue and stock movements, including image uploads",
        _ => "Queue"
    };
}

/// <summary>
/// The payload written to a queue message.
/// </summary>
/// <remarks>
/// A queue message is just a string, so the payload is serialised as JSON. It carries a
/// human readable <see cref="Message"/> for the operator alongside the fields a consumer
/// needs to act on it, which means the same message serves the monitoring screen and the
/// processing code without a second lookup.
/// </remarks>
public class QueueOperation
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Short machine readable operation name, for example OrderPlaced.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Readable description, for example Processing order ORD-000004.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Identifier the consumer needs, typically an order or product key.</summary>
    public string? Reference { get; set; }

    public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>
    /// Reads a payload back. Messages placed on the queue by another tool will not be JSON,
    /// so anything that fails to parse is surfaced as a plain message rather than discarded.
    /// </summary>
    public static QueueOperation FromJson(string raw)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<QueueOperation>(raw, Options);
            if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.Message))
            {
                return parsed;
            }
        }
        catch (JsonException)
        {
            // Falls through to the plain text representation below.
        }

        return new QueueOperation { Kind = "Unstructured", Message = raw };
    }
}

/// <summary>One message as read from a queue, with the queue metadata the view needs.</summary>
public record QueuedMessage(
    string MessageId,
    QueueOperation Operation,
    DateTimeOffset? InsertedOn,
    long DequeueCount,
    string RawBody);
