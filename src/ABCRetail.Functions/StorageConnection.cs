// ABC Retail - Azure Functions
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

namespace ABCRetail.Functions;

/// <summary>
/// Resolves the storage connection string the functions run against.
/// </summary>
/// <remarks>
/// The Function App is created against the same storage account the web application
/// uses, so AzureWebJobsStorage already points at it and no second secret has to be
/// managed. StorageConnection is checked first so the value can be overridden without
/// disturbing the setting the Functions runtime itself depends on.
/// </remarks>
internal static class StorageConnection
{
    public static string Resolve()
    {
        var value = Environment.GetEnvironmentVariable("StorageConnection")
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage");

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "No storage connection string is configured. Set StorageConnection or AzureWebJobsStorage.");
        }

        return value;
    }
}
