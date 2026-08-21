// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

namespace ABCRetail.Web.Models;

/// <summary>One queue and the messages currently sitting on it.</summary>
public class QueueSnapshot
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int ApproximateCount { get; set; }

    public IReadOnlyList<QueuedMessage> Messages { get; set; } = [];
}

/// <summary>The queue monitoring screen.</summary>
public class QueueMonitorViewModel
{
    public IReadOnlyList<QueueSnapshot> Queues { get; set; } = [];

    public int TotalDepth => Queues.Sum(q => q.ApproximateCount);
}

/// <summary>One file held in the Azure file share.</summary>
public record ShareFileEntry(string Name, string Directory, long SizeBytes, DateTimeOffset? LastModified)
{
    public string Path => $"{Directory}/{Name}";

    public string ReadableSize => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:N1} KB",
        _ => $"{SizeBytes / 1024.0 / 1024.0:N1} MB"
    };
}

/// <summary>The Azure Files browsing screen.</summary>
public class FileShareViewModel
{
    public string ShareName { get; set; } = string.Empty;

    public IReadOnlyList<ShareFileEntry> LogFiles { get; set; } = [];

    public IReadOnlyList<ShareFileEntry> Exports { get; set; } = [];

    public string? SelectedPath { get; set; }

    public IReadOnlyList<LogLine> SelectedContent { get; set; } = [];

    public long TotalBytes => LogFiles.Sum(f => f.SizeBytes) + Exports.Sum(f => f.SizeBytes);
}

/// <summary>A single parsed line from a log file, so the viewer can colour severities.</summary>
public record LogLine(string Timestamp, string Level, string Text)
{
    /// <summary>
    /// Log lines are written as "timestamp [LEVEL] text". Anything that does not match that
    /// shape is shown verbatim rather than dropped.
    /// </summary>
    public static LogLine Parse(string raw)
    {
        var open = raw.IndexOf('[');
        var close = raw.IndexOf(']');

        if (open > 0 && close > open)
        {
            return new LogLine(
                raw[..open].Trim(),
                raw[(open + 1)..close].Trim(),
                raw[(close + 1)..].Trim());
        }

        return new LogLine(string.Empty, string.Empty, raw);
    }
}
