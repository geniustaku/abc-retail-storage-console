// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using ABCRetail.Web.Services;

namespace ABCRetail.Web.Models;

/// <summary>The Azure Functions screen.</summary>
public class FunctionsViewModel
{
    public bool IsConfigured { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public IReadOnlyList<FunctionCallResult> Results { get; set; } = [];

    public bool HasRun => Results.Count > 0;

    public int SuccessCount => Results.Count(r => r.Success);
}
