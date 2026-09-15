using System.Globalization;

namespace Domolov.Domain.Services;

/// <summary>Formats listing prices for UI and notifications.</summary>
public static class PriceFormatting
{
    private static readonly CultureInfo Slovenian = CultureInfo.GetCultureInfo("sl-SI");

    /// <summary>
    /// Compact Slovenian-style amount with currency symbol, e.g. <c>210.000 €</c>.
    /// Whole euros omit decimals; otherwise two fractional digits.
    /// </summary>
    public static string Format(decimal amount, string? currency = "EUR")
    {
        var symbol = CurrencySymbol(currency);
        var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        var text =
            rounded == decimal.Truncate(rounded)
                ? rounded.ToString("#,0", Slovenian)
                : rounded.ToString("#,0.00", Slovenian);
        return $"{text} {symbol}";
    }

    private static string CurrencySymbol(string? currency) =>
        string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(currency)
            ? "€"
            : currency.Trim().ToUpperInvariant();
}
