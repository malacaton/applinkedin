using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Ali.Core.Content;

/// <summary>
/// Turns the YAML of a front matter block into a <see cref="Piece"/>, validating field by field.
/// </summary>
internal static class MetadataParser
{
    public const string SeriesField = "serie";
    public const string StatusField = "estado";
    public const string PlannedDateField = "fecha_prevista";
    public const string PublishedDateField = "fecha_publicada";
    public const string UrlField = "url";
    public const string DependsOnField = "depende_de";
    public const string FormatField = "formato";

    private const string DateFormat = "yyyy-MM-dd";

    // The opening '---' line comes before the YAML text.
    private const int FrontMatterLineOffset = 1;

    public static Piece Parse(string path, string yaml, ICollection<CatalogWarning> warnings)
    {
        var bare = new Piece { Path = path };

        YamlNode? root;
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            root = stream.Documents.Count > 0 ? stream.Documents[0].RootNode : null;
        }
        catch (YamlException ex)
        {
            warnings.Add(new CatalogWarning(
                path,
                CatalogWarningCode.FrontMatterInvalid,
                null,
                $"YAML no válido en la línea {ex.Start.Line + FrontMatterLineOffset}, columna {ex.Start.Column}: {Describe(ex)}"));
            return bare;
        }
        catch (ArgumentException ex)
        {
            // YamlDotNet reports duplicate mapping keys this way.
            warnings.Add(new CatalogWarning(
                path,
                CatalogWarningCode.FrontMatterInvalid,
                null,
                $"YAML no válido: {ex.Message}"));
            return bare;
        }

        if (root is null || IsNull(root))
        {
            return bare with { HasFrontMatter = true };
        }

        if (root is not YamlMappingNode mapping)
        {
            warnings.Add(new CatalogWarning(
                path,
                CatalogWarningCode.FrontMatterNotMapping,
                null,
                "El bloque YAML debe ser una lista de pares clave: valor."));
            return bare;
        }

        var fields = new FieldReader(path, mapping, warnings);
        return bare with
        {
            HasFrontMatter = true,
            Series = fields.Text(SeriesField),
            Status = fields.Status(),
            PlannedDate = fields.Date(PlannedDateField),
            PublishedDate = fields.Date(PublishedDateField),
            Url = fields.Text(UrlField),
            DependsOn = fields.TextList(DependsOnField),
            Format = fields.Text(FormatField),
        };
    }

    private static string Describe(YamlException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Trim();
    }

    private static bool IsNull(YamlNode node) =>
        node is YamlScalarNode { Style: ScalarStyle.Plain } scalar
        && scalar.Value is null or "" or "~" or "null" or "Null" or "NULL";

    private sealed class FieldReader(string path, YamlMappingNode mapping, ICollection<CatalogWarning> warnings)
    {
        public string? Text(string field)
        {
            var node = Find(field);
            if (node is null)
            {
                return null;
            }

            if (node is not YamlScalarNode scalar)
            {
                warnings.Add(new CatalogWarning(
                    path,
                    CatalogWarningCode.InvalidFieldType,
                    field,
                    $"El campo '{field}' debe ser un texto, no una lista ni un bloque."));
                return null;
            }

            var value = scalar.Value?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        public PieceStatus? Status()
        {
            var value = Text(StatusField);
            if (value is null)
            {
                return null;
            }

            if (PieceStatusNames.TryParse(value, out var status))
            {
                return status;
            }

            warnings.Add(new CatalogWarning(
                path,
                CatalogWarningCode.InvalidStatus,
                StatusField,
                $"Estado '{value}' no válido. Valores admitidos: {string.Join(", ", PieceStatusNames.All)}."));
            return null;
        }

        public DateOnly? Date(string field)
        {
            var value = Text(field);
            if (value is null)
            {
                return null;
            }

            if (DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }

            warnings.Add(new CatalogWarning(
                path,
                CatalogWarningCode.InvalidDate,
                field,
                $"Fecha '{value}' no válida en '{field}'. Formato esperado: AAAA-MM-DD."));
            return null;
        }

        public IReadOnlyList<string> TextList(string field)
        {
            var node = Find(field);
            switch (node)
            {
                case null:
                    return [];
                case YamlScalarNode:
                    return Text(field) is { } single ? [single] : [];
                case YamlSequenceNode sequence:
                    var items = new List<string>();
                    foreach (var item in sequence.Children)
                    {
                        if (item is not YamlScalarNode scalar)
                        {
                            warnings.Add(new CatalogWarning(
                                path,
                                CatalogWarningCode.InvalidFieldType,
                                field,
                                $"Cada elemento de '{field}' debe ser un texto."));
                            return [];
                        }

                        var value = scalar.Value?.Trim();
                        if (!string.IsNullOrEmpty(value) && !IsNull(scalar))
                        {
                            items.Add(value);
                        }
                    }

                    return items;
                default:
                    warnings.Add(new CatalogWarning(
                        path,
                        CatalogWarningCode.InvalidFieldType,
                        field,
                        $"El campo '{field}' debe ser un texto o una lista de textos."));
                    return [];
            }
        }

        private YamlNode? Find(string field)
        {
            var key = new YamlScalarNode(field);
            if (!mapping.Children.TryGetValue(key, out var node))
            {
                return null;
            }

            return IsNull(node) ? null : node;
        }
    }
}
