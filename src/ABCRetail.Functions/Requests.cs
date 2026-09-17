// ABC Retail - Azure Functions
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

namespace ABCRetail.Functions;

public class TableEntityRequest
{
    public string? Table { get; set; }
    public string? PartitionKey { get; set; }
    public string? RowKey { get; set; }
    public Dictionary<string, string>? Properties { get; set; }
}

public class BlobRequest
{
    public string? Container { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }

    /// <summary>File contents encoded as base64 so they survive a JSON request body.</summary>
    public string? ContentBase64 { get; set; }
}

public class QueueRequest
{
    public string? Queue { get; set; }
    public string? Message { get; set; }
    public string? Reference { get; set; }
}

public class FileRequest
{
    public string? Directory { get; set; }
    public string? FileName { get; set; }
    public string? Line { get; set; }
}
