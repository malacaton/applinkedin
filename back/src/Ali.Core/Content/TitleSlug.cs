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
    /// Maximum length of <see cref="Original"/>.
    /// </summary>
    public const int MaxOriginalLength = 120;

    // Not allowed in file names on Windows, plus the Spanish opening marks that go with '?' and '!'.
    private static readonly char[] RemovedFromOriginal = ['\\', '/', ':', '*', '?', '"', '<', '>', '|', '¿', '¡'];

    /// <summary>
    /// The title as typed, only without characters a file name cannot hold:
    /// "¿Por qué existe esta función?" → "Por qué existe esta función". Empty when nothing is left.
    /// </summary>
    public static string Original(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        var builder = new StringBuilder(title.Length);
        var pendingSpace = false;
        foreach (var c in title)
        {
            if (Array.IndexOf(RemovedFromOriginal, c) >= 0 || char.IsControl(c))
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        var original = builder.ToString().Trim(' ', '.');
        if (original.Length > MaxOriginalLength)
        {
            original = original[..MaxOriginalLength].TrimEnd(' ', '.');
        }

        return original;
    }

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
