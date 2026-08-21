// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using ABCRetail.Web.Models;

namespace ABCRetail.Web.Services;

/// <summary>
/// Contract over the Azure file share that holds the application's log files and any
/// reports written on demand.
/// </summary>
public interface IFileShareService
{
    /// <summary>Directory holding the daily application logs.</summary>
    const string LogsDirectory = "logs";

    /// <summary>Directory holding generated reports.</summary>
    const string ExportsDirectory = "exports";

    string ShareName { get; }

    /// <summary>Appends one line to today's log file, creating the file on first write.</summary>
    Task AppendLogAsync(string level, string message, CancellationToken cancellationToken = default);

    /// <summary>Writes a file, replacing any existing file of the same name.</summary>
    Task WriteFileAsync(string directory, string fileName, byte[] content, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShareFileEntry>> ListAsync(string directory, CancellationToken cancellationToken = default);

    /// <summary>Reads a file's bytes. Returns null when the file does not exist.</summary>
    Task<byte[]?> ReadAsync(string directory, string fileName, CancellationToken cancellationToken = default);

    Task<long> TotalSizeAsync(CancellationToken cancellationToken = default);
}
