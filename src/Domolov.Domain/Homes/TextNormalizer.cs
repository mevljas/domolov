using System.Globalization;
using System.Text;

namespace Domolov.Domain.Homes;

/// <summary>Lowercases, folds diacritics (č → c) and strips punctuation for text comparison.</summary>
public static class TextNormalizer
{
    public static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var decomposed = text.ToLowerInvariant()
            .Replace('đ', 'd')
            .Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var lastWasSpace = true;
        foreach (var c in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                sb.Append(' ');
                lastWasSpace = true;
            }
        }

        var result = sb.ToString().Trim();
        return result.Length == 0 ? null : result;
    }
}
