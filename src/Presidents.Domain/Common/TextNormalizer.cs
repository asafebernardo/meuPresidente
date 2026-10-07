using System.Globalization;
using System.Text;

namespace Presidents.Domain.Common;

public static class TextNormalizer
{
    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string Slugify(string? value)
    {
        var folded = Fold(value);
        var builder = new StringBuilder(folded.Length);
        var pendingHyphen = false;
        foreach (var character in folded)
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
                pendingHyphen = false;
            }
            else if (!pendingHyphen && builder.Length > 0)
            {
                builder.Append('-');
                pendingHyphen = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    public static string Digits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value.Where(char.IsDigit).ToArray());
    }
}
