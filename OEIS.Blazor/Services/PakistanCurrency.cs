using System.Globalization;

namespace OEIS.Blazor.Services;

/// <summary>
/// Formats monetary values as Pakistani Rupees (PKR) for UI display.
/// Storage remains <see cref="decimal"/>; culture is presentation-only.
/// </summary>
public static class PakistanCurrency
{
    private static readonly CultureInfo PkrCulture = CultureInfo.GetCultureInfo("en-PK");

    public static string Format(decimal amount) => amount.ToString("C", PkrCulture);

    public static string Format(decimal? amount) => amount.HasValue ? Format(amount.Value) : "—";
}
