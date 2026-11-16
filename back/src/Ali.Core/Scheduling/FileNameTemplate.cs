using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ali.Core.Scheduling;

/// <summary>
/// Values that fill a <see cref="FileNameTemplate"/>.
/// </summary>
/// <param name="Number">Order number ({n}, {nn}, {nnn}).</param>
/// <param name="TitleSlug">Title as a slug ({titulo}).</param>
/// <param name="OriginalTitle">Title as typed, cleaned for the file system ({titulo_original}).</param>
/// <param name="Date">Planned date ({fecha}, {aammdd}).</param>
public sealed record FileNameValues(int Number, string TitleSlug, string OriginalTitle, DateOnly? Date);

/// <summary>
/// Result of reading a file name with a template.
/// </summary>
/// <param name="Number">Order number, when the template has a number placeholder.</param>
/// <param name="DateEnd">Position just after the last date placeholder, or -1 when the template has no date.</param>
public sealed record FileNameMatch(int? Number, int DateEnd);

/// <summary>
/// File name pattern of a series, without the .md extension.
/// Placeholders: {n}, {nn}, {nnn}, {titulo}, {titulo_original}, {fecha} (AAAA-MM-DD) and {aammdd}.
/// </summary>
public sealed class FileNameTemplate : IEquatable<FileNameTemplate>
{
    public const string TitleToken = "titulo";
    public const string OriginalTitleToken = "titulo_original";
    public const string DateToken = "fecha";
    public const string ShortDateToken = "aammdd";

    private const string IsoDateFormat = "yyyy-MM-dd";
    private const string ShortDateFormat = "yyMMdd";

    private static readonly Dictionary<string, int> NumberTokens = new(StringComparer.Ordinal)
    {
        ["n"] = 1,
        ["nn"] = 2,
        ["nnn"] = 3,
    };

    private static readonly HashSet<string> KnownTokens = [TitleToken, OriginalTitleToken, DateToken, ShortDateToken, .. NumberTokens.Keys];

    private readonly List<Part> parts;
    private readonly Regex pattern;

    private FileNameTemplate(string text, List<Part> parts)
    {
        Text = text;
        this.parts = parts;
        pattern = BuildPattern(parts);
    }

    /// <summary>
    /// '{nn}-{titulo}'.
    /// </summary>
    public static FileNameTemplate Default { get; } = Parse("{nn}-{titulo}");

    public string Text { get; }

    public bool UsesDate => parts.Any(p => p.Token is DateToken or ShortDateToken);

    public bool UsesNumber => parts.Any(p => p.Token is not null && NumberTokens.ContainsKey(p.Token));

    /// <summary>
    /// Parses <paramref name="text"/>; on failure, <paramref name="error"/> says why, in Spanish.
    /// </summary>
    public static bool TryParse(string text, out FileNameTemplate? template, out string? error)
    {
        template = null;
        error = null;

        var parts = new List<Part>();
        var literal = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '}')
            {
                error = "tiene una '}' sin su '{'.";
                return false;
            }

            if (c != '{')
            {
                literal.Append(c);
                i++;
                continue;
            }

            var end = text.IndexOf('}', i + 1);
            if (end < 0)
            {
                error = "tiene una '{' sin cerrar.";
                return false;
            }

            var token = text[(i + 1)..end];
            if (!KnownTokens.Contains(token))
            {
                error = $"marcador '{{{token}}}' desconocido; usa {{n}}, {{nn}}, {{nnn}}, {{titulo}}, {{titulo_original}}, {{fecha}} o {{aammdd}}.";
                return false;
            }

            if (literal.Length > 0)
            {
                parts.Add(new Part(literal.ToString(), null));
                literal.Clear();
            }

            parts.Add(new Part(null, token));
            i = end + 1;
        }

        if (literal.Length > 0)
        {
            parts.Add(new Part(literal.ToString(), null));
        }

        if (!parts.Any(p => p.Token is TitleToken or OriginalTitleToken))
        {
            error = "debe incluir {titulo} o {titulo_original}.";
            return false;
        }

        var literalText = string.Concat(parts.Where(p => p.Literal is not null).Select(p => p.Literal));
        if (literalText.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || text.StartsWith('.'))
        {
            error = "solo puede contener el nombre del fichero, sin carpetas ni caracteres reservados.";
            return false;
        }

        template = new FileNameTemplate(text, parts);
        return true;
    }

    private static FileNameTemplate Parse(string text) =>
        TryParse(text, out var template, out var error) ? template! : throw new FormatException(error);

    /// <summary>
    /// File name without extension, using the slug as original title too.
    /// </summary>
    public string Resolve(int number, string titleSlug, DateOnly? date) =>
        Resolve(new FileNameValues(number, titleSlug, titleSlug, date));

    /// <summary>
    /// File name without extension.
    /// </summary>
    public string Resolve(FileNameValues values)
    {
        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            builder.Append(part.Token switch
            {
                null => part.Literal,
                TitleToken => values.TitleSlug,
                OriginalTitleToken => values.OriginalTitle,
                DateToken => FormatDate(RequireDate(values.Date), IsoDateFormat),
                ShortDateToken => FormatDate(RequireDate(values.Date), ShortDateFormat),
                _ => values.Number.ToString(CultureInfo.InvariantCulture).PadLeft(NumberTokens[part.Token], '0'),
            });
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reads a file name (without extension) with this template. Fails when it does not fit,
    /// or when the number placeholders hold different numbers.
    /// </summary>
    public bool TryMatch(string name, out FileNameMatch? match)
    {
        match = null;
        var result = pattern.Match(name);
        if (!result.Success)
        {
            return false;
        }

        int? number = null;
        var dateEnd = -1;
        for (var index = 0; index < parts.Count; index++)
        {
            var token = parts[index].Token;
            if (token is null)
            {
                continue;
            }

            var group = result.Groups[GroupName(index)];
            if (NumberTokens.ContainsKey(token))
            {
                if (!int.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                    || (number is { } previous && previous != value))
                {
                    return false;
                }

                number = value;
            }
            else if (token is DateToken or ShortDateToken)
            {
                if (!TryParseDate(group.Value, token, out _))
                {
                    return false;
                }

                dateEnd = Math.Max(dateEnd, group.Index + group.Length);
            }
        }

        match = new FileNameMatch(number, dateEnd);
        return true;
    }

    /// <summary>
    /// The same file name with every date placeholder set to <paramref name="date"/>; null when the name does not fit.
    /// </summary>
    public string? ReplaceDate(string name, DateOnly date)
    {
        if (!UsesDate || !TryMatch(name, out _))
        {
            return null;
        }

        var result = pattern.Match(name);
        var builder = new StringBuilder(name);
        var replacements = parts
            .Select((part, index) => (part.Token, Group: result.Groups[GroupName(index)]))
            .Where(x => x.Token is DateToken or ShortDateToken)
            .OrderByDescending(x => x.Group.Index);
        foreach (var (token, group) in replacements)
        {
            builder.Remove(group.Index, group.Length);
            builder.Insert(group.Index, FormatDate(date, token == DateToken ? IsoDateFormat : ShortDateFormat));
        }

        return builder.ToString();
    }

    private static Regex BuildPattern(List<Part> parts)
    {
        var builder = new StringBuilder("^");
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            builder.Append(part.Token switch
            {
                null => Regex.Escape(part.Literal!),
                TitleToken or OriginalTitleToken => $"(?<{GroupName(index)}>.+?)",
                DateToken => $"(?<{GroupName(index)}>\\d{{4}}-\\d{{2}}-\\d{{2}})",
                ShortDateToken => $"(?<{GroupName(index)}>\\d{{6}})",
                _ => $"(?<{GroupName(index)}>\\d+)",
            });
        }

        builder.Append('$');
        return new Regex(builder.ToString(), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

    private static string GroupName(int index) => $"p{index}";

    private static DateOnly RequireDate(DateOnly? date) =>
        date ?? throw new InvalidOperationException("The template uses a date and no date was given.");

    private static string FormatDate(DateOnly date, string format) => date.ToString(format, CultureInfo.InvariantCulture);

    private static bool TryParseDate(string value, string token, out DateOnly date) =>
        DateOnly.TryParseExact(
            value,
            token == DateToken ? IsoDateFormat : ShortDateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);

    public bool Equals(FileNameTemplate? other) => other is not null && Text == other.Text;

    public override bool Equals(object? obj) => Equals(obj as FileNameTemplate);

    public override int GetHashCode() => Text.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Text;

    private sealed record Part(string? Literal, string? Token);
}
