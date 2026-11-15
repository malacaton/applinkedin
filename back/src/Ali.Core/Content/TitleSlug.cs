using System.Globalization;
using System.Text;

namespace Ali.Core.Content;

/// <summary>
/// Turns a title into the part of a file name that represents it: "Qué aprendimos del RAG" → "que-aprendimos-del-rag".
/// </summary>
public static class TitleSlug
{
    public const int MaxLength = 60;

    /// <summary>
    /// Lowercase ASCII letters and digits separated by single hyphens; empty when the title has no letters or digits.
    /// </summary>
    public static string From(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        var decomposed = title.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingHyphen = false;

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                // Accents, diaeresis and the tilde of 'ñ' after decomposition.
                continue;
            }

            var lower = char.ToLowerInvariant(c);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                }

                pendingHyphen = false;
                builder.Append(lower);
            }
            else
            {
                pendingHyphen = true;
            }
        }

        var slug = builder.ToString();
        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength].TrimEnd('-');
        }

        return slug;
    }
}
