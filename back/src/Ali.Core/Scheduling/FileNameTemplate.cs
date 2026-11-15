using System.Globalization;
using System.Text;

namespace Ali.Core.Scheduling;

/// <summary>
/// File name pattern of a series, without the .md extension. Placeholders: {n}, {nn}, {nnn}, {titulo}, {fecha}.
/// </summary>
public sealed class FileNameTemplate : IEquatable<FileNameTemplate>
{
    public const string TitleToken = "titulo";
    public const string DateToken = "fecha";

    private static readonly Dictionary<string, int> NumberTokens = new(StringComparer.Ordinal)
    {
        ["n"] = 1,
        ["nn"] = 2,
        ["nnn"] = 3,
    };

    private readonly List<Part> parts;

    private FileNameTemplate(string text, List<Part> parts)
    {
        Text = text;
        this.parts = parts;
    }

    /// <summary>
    /// '{nn}-{titulo}'.
    /// </summary>
    public static FileNameTemplate Default { get; } = Parse("{nn}-{titulo}");

    public string Text { get; }

    public bool UsesDate => parts.Any(p => p.Token == DateToken);

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
            if (token != TitleToken && token != DateToken && !NumberTokens.ContainsKey(token))
            {
                error = $"marcador '{{{token}}}' desconocido; usa {{n}}, {{nn}}, {{nnn}}, {{titulo}} o {{fecha}}.";
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

        if (!parts.Any(p => p.Token == TitleToken))
        {
            error = "debe incluir {titulo}.";
            return false;
        }

        var literalText = string.Concat(parts.Where(p => p.Literal is not null).Select(p => p.Literal));
        if (literalText.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || literalText.StartsWith('.'))
        {
            error = "solo puede contener el nombre del fichero, sin carpetas ni caracteres reservados.";
            return false;
        }

        if (text.StartsWith('.'))
        {
            error = "no puede empezar por '.'.";
            return false;
        }

        template = new FileNameTemplate(text, parts);
        return true;
    }

    private static FileNameTemplate Parse(string text) =>
        TryParse(text, out var template, out var error) ? template! : throw new FormatException(error);

    /// <summary>
    /// File name without extension.
    /// </summary>
    public string Resolve(int number, string titleSlug, DateOnly? date)
    {
        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Literal is not null)
            {
                builder.Append(part.Literal);
            }
            else if (part.Token == TitleToken)
            {
                builder.Append(titleSlug);
            }
            else if (part.Token == DateToken)
            {
                var value = date ?? throw new InvalidOperationException("The template uses {fecha} and no date was given.");
                builder.Append(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(number.ToString(CultureInfo.InvariantCulture).PadLeft(NumberTokens[part.Token!], '0'));
            }
        }

        return builder.ToString();
    }

    public bool Equals(FileNameTemplate? other) => other is not null && Text == other.Text;

    public override bool Equals(object? obj) => Equals(obj as FileNameTemplate);

    public override int GetHashCode() => Text.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Text;

    private sealed record Part(string? Literal, string? Token);
}
