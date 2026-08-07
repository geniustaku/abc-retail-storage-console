// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using System.Globalization;

namespace ABCRetail.Web.Models;

/// <summary>
/// Formats amounts as South African rand.
/// </summary>
/// <remarks>
/// The application pins itself to the invariant culture so that values posted by number
/// inputs round trip unchanged whatever locale the host machine carries. Presentation
/// formatting is therefore applied deliberately here rather than inherited from the
/// thread, which also keeps the rendering identical locally and on App Service.
/// </remarks>
public static class Money
{
    private const string NoBreakSpace = " ";

    private static readonly NumberFormatInfo Rand = new()
    {
        NumberGroupSeparator = NoBreakSpace,
        NumberDecimalSeparator = ".",
        NumberGroupSizes = [3],
        NumberDecimalDigits = 2
    };

    /// <summary>Renders an amount to the cent, for example R 1 899.00.</summary>
    public static string Format(double amount) => $"R{NoBreakSpace}{amount.ToString("N2", Rand)}";

    /// <summary>Renders an amount to the nearest rand, for summary figures.</summary>
    public static string FormatWhole(double amount) => $"R{NoBreakSpace}{amount.ToString("N0", Rand)}";
}
