using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Ali.Core.Scheduling;

/// <summary>
/// Loads and validates the series configuration file (YAML). Never modifies the file.
/// </summary>
public sealed partial class SeriesConfigLoader
{
    private const string RootKey = "series";
    private const string IdKey = "id";
    private const string NameKey = "nombre";
    private const string CadenceKey = "cadencia";
    private const string AfterKey = "despues_de";
    private const string DaysKey = "dias";
    private const string FrequencyKey = "frecuencia";
    private const string AnchorKey = "desde";
    private const string TimeKey = "hora";
    private const string LinkSeriesKey = "serie";

    private const int MinLinkDays = 1;
    private const int MaxLinkDays = 60;

    private static readonly string[] SeriesKeys = [IdKey, NameKey, CadenceKey, AfterKey];
    private static readonly string[] CadenceKeys = [DaysKey, FrequencyKey, AnchorKey, TimeKey];
    private static readonly string[] LinkKeys = [LinkSeriesKey, DaysKey];
    private static readonly string[] TimeFormats = ["HH:mm", "H:mm"];

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IdPattern();

    /// <summary>
    /// Loads the file at <paramref name="path"/>.
    /// </summary>
    /// <exception cref="SeriesConfigException">The file is missing, unreadable or invalid. Lists every error.</exception>
    public SeriesConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw Fail(path ?? string.Empty, "La ruta del fichero de series es obligatoria.");
        }

        var fullPath = System.IO.Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw Fail(fullPath, $"No existe el fichero de series: {fullPath}");
        }

        string text;
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            text = reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Fail(fullPath, $"No se pudo leer el fichero de series: {ex.Message}");
        }

        YamlNode? root;
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(text));
            root = yaml.Documents.Count > 0 ? yaml.Documents[0].RootNode : null;
        }
        catch (YamlException ex)
        {
            var reason = (ex.InnerException?.Message ?? ex.Message).Trim();
            throw Fail(fullPath, $"YAML no válido en la línea {ex.Start.Line}, columna {ex.Start.Column}: {reason}");
        }
        catch (ArgumentException ex)
        {
            throw Fail(fullPath, $"YAML no válido: {ex.Message}");
        }

        var errors = new List<SeriesConfigError>();
        var drafts = ReadSeriesList(root, errors);
        ValidateRelations(drafts, errors);

        if (errors.Count > 0)
        {
            throw new SeriesConfigException(fullPath, errors);
        }

        return new SeriesConfig(drafts.Select(d => new Series(d.Id!, d.Name!, d.Cadence, d.After)).ToList());
    }

    private static SeriesConfigException Fail(string path, string reason) =>
        new(path, [new SeriesConfigError(null, null, reason)]);

    // ---------- First pass: each series on its own ----------

    private static List<SeriesDraft> ReadSeriesList(YamlNode? root, List<SeriesConfigError> errors)
    {
        const string missingRoot = "El fichero debe tener una clave 'series' con la lista de series.";

        if (root is not YamlMappingNode rootMapping)
        {
            errors.Add(new SeriesConfigError(null, null, missingRoot));
            return [];
        }

        ReportUnknownKeys(rootMapping, [RootKey], null, null, errors);

        if (Get(rootMapping, RootKey) is not YamlSequenceNode list)
        {
            // Plain 'series:' with no items is an empty list.
            if (Has(rootMapping, RootKey) && Get(rootMapping, RootKey) is null)
            {
                return [];
            }

            errors.Add(new SeriesConfigError(null, null, missingRoot));
            return [];
        }

        var drafts = new List<SeriesDraft>();
        var position = 0;
        foreach (var item in list.Children)
        {
            position++;
            drafts.Add(ReadSeries(item, position, errors));
        }

        return drafts;
    }

    private static SeriesDraft ReadSeries(YamlNode node, int position, List<SeriesConfigError> errors)
    {
        var draft = new SeriesDraft(position);

        if (node is not YamlMappingNode mapping)
        {
            errors.Add(new SeriesConfigError(draft.Label, null, "debe ser un bloque de pares clave: valor."));
            return draft;
        }

        // The id goes first so the label of later errors can use it.
        var id = ReadText(mapping, IdKey, draft.Label, errors);
        if (id is null)
        {
            if (!HasError(errors, draft.Label, IdKey))
            {
                errors.Add(new SeriesConfigError(draft.Label, IdKey, "es obligatorio."));
            }
        }
        else if (!IdPattern().IsMatch(id))
        {
            errors.Add(new SeriesConfigError(draft.Label, IdKey, $"'{id}' no es válido; usa minúsculas, dígitos y guiones."));
        }
        else
        {
            draft.Id = id;
        }

        var label = draft.Label;
        ReportUnknownKeys(mapping, SeriesKeys, label, null, errors);

        draft.Name = ReadText(mapping, NameKey, label, errors);
        if (draft.Name is null && !HasError(errors, label, NameKey))
        {
            errors.Add(new SeriesConfigError(label, NameKey, "es obligatorio."));
        }

        draft.Cadence = ReadCadence(mapping, label, errors);
        draft.After = ReadLink(mapping, label, errors);
        return draft;
    }

    private static Cadence? ReadCadence(YamlMappingNode series, string label, List<SeriesConfigError> errors)
    {
        var node = Get(series, CadenceKey);
        if (node is null)
        {
            return null;
        }

        if (node is not YamlMappingNode mapping)
        {
            errors.Add(new SeriesConfigError(label, CadenceKey, "debe ser un bloque con 'dias' y 'frecuencia'."));
            return null;
        }

        var errorsBefore = errors.Count;
        ReportUnknownKeys(mapping, CadenceKeys, label, CadenceKey, errors);

        var days = ReadDays(mapping, label, errors);
        var frequency = ReadFrequency(mapping, label, errors);
        var anchor = ReadAnchor(mapping, frequency, label, errors);
        var time = ReadTime(mapping, label, errors);

        return errors.Count == errorsBefore && days is not null && frequency is not null
            ? new Cadence(days, frequency.Value, anchor, time)
            : null;
    }

    private static List<DayOfWeek>? ReadDays(YamlMappingNode cadence, string label, List<SeriesConfigError> errors)
    {
        const string field = CadenceKey + "." + DaysKey;
        var node = Get(cadence, DaysKey);

        IEnumerable<YamlNode> items;
        switch (node)
        {
            case null:
                errors.Add(new SeriesConfigError(label, field, "es obligatorio."));
                return null;
            case YamlScalarNode:
                items = [node];
                break;
            case YamlSequenceNode sequence:
                items = sequence.Children;
                break;
            default:
                errors.Add(new SeriesConfigError(label, field, "debe ser un día o una lista de días."));
                return null;
        }

        var days = new List<DayOfWeek>();
        var valid = true;
        foreach (var item in items)
        {
            if (item is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
            {
                errors.Add(new SeriesConfigError(label, field, "cada día debe ser un texto, como 'martes'."));
                valid = false;
                continue;
            }

            var value = scalar.Value.Trim();
            if (!SpanishDays.TryParse(value, out var day))
            {
                errors.Add(new SeriesConfigError(label, field, $"'{value}' no es un día de la semana."));
                valid = false;
            }
            else if (days.Contains(day))
            {
                errors.Add(new SeriesConfigError(label, field, $"'{value}' está repetido."));
                valid = false;
            }
            else
            {
                days.Add(day);
            }
        }

        if (valid && days.Count == 0)
        {
            errors.Add(new SeriesConfigError(label, field, "debe tener al menos un día."));
            return null;
        }

        return valid ? days : null;
    }

    private static CadenceFrequency? ReadFrequency(YamlMappingNode cadence, string label, List<SeriesConfigError> errors)
    {
        const string field = CadenceKey + "." + FrequencyKey;
        var value = ReadText(cadence, FrequencyKey, label, errors, field);
        if (value is null)
        {
            if (!HasError(errors, label, field))
            {
                errors.Add(new SeriesConfigError(label, field, "es obligatorio."));
            }

            return null;
        }

        switch (value.ToLowerInvariant())
        {
            case "semanal":
                return CadenceFrequency.Weekly;
            case "quincenal":
                return CadenceFrequency.Biweekly;
            default:
                errors.Add(new SeriesConfigError(label, field, $"'{value}' no es válida; usa semanal o quincenal."));
                return null;
        }
    }

    private static DateOnly? ReadAnchor(
        YamlMappingNode cadence,
        CadenceFrequency? frequency,
        string label,
        List<SeriesConfigError> errors)
    {
        const string field = CadenceKey + "." + AnchorKey;
        var value = ReadText(cadence, AnchorKey, label, errors, field);
        if (value is null)
        {
            if (frequency == CadenceFrequency.Biweekly && !HasError(errors, label, field))
            {
                errors.Add(new SeriesConfigError(label, field, "es obligatorio con frecuencia quincenal."));
            }

            return null;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        errors.Add(new SeriesConfigError(label, field, $"'{value}' no es una fecha AAAA-MM-DD válida."));
        return null;
    }

    private static TimeOnly? ReadTime(YamlMappingNode cadence, string label, List<SeriesConfigError> errors)
    {
        const string field = CadenceKey + "." + TimeKey;
        var value = ReadText(cadence, TimeKey, label, errors, field);
        if (value is null)
        {
            return null;
        }

        if (TimeOnly.TryParseExact(value, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return time;
        }

        errors.Add(new SeriesConfigError(label, field, $"'{value}' no es una hora HH:mm válida."));
        return null;
    }

    private static SeriesLink? ReadLink(YamlMappingNode series, string label, List<SeriesConfigError> errors)
    {
        var node = Get(series, AfterKey);
        if (node is null)
        {
            return null;
        }

        if (node is not YamlMappingNode mapping)
        {
            errors.Add(new SeriesConfigError(label, AfterKey, "debe ser un bloque con 'serie' y 'dias'."));
            return null;
        }

        ReportUnknownKeys(mapping, LinkKeys, label, AfterKey, errors);

        const string seriesField = AfterKey + "." + LinkSeriesKey;
        const string daysField = AfterKey + "." + DaysKey;

        var target = ReadText(mapping, LinkSeriesKey, label, errors, seriesField);
        if (target is null && !HasError(errors, label, seriesField))
        {
            errors.Add(new SeriesConfigError(label, seriesField, "es obligatorio."));
        }

        int? days = null;
        var daysText = ReadText(mapping, DaysKey, label, errors, daysField);
        if (daysText is null)
        {
            if (!HasError(errors, label, daysField))
            {
                errors.Add(new SeriesConfigError(label, daysField, "es obligatorio."));
            }
        }
        else if (int.TryParse(daysText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                 && parsed is >= MinLinkDays and <= MaxLinkDays)
        {
            days = parsed;
        }
        else
        {
            errors.Add(new SeriesConfigError(
                label,
                daysField,
                $"debe ser un entero entre {MinLinkDays} y {MaxLinkDays}."));
        }

        return target is not null && days is not null ? new SeriesLink(target, days.Value) : null;
    }

    // ---------- Second pass: relations between series ----------

    private static void ValidateRelations(List<SeriesDraft> drafts, List<SeriesConfigError> errors)
    {
        var firstPositionById = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var draft in drafts.Where(d => d.Id is not null))
        {
            if (firstPositionById.TryGetValue(draft.Id!, out var first))
            {
                errors.Add(new SeriesConfigError(
                    draft.Label,
                    IdKey,
                    $"está repetido (series n.º {first} y n.º {draft.Position})."));
            }
            else
            {
                firstPositionById[draft.Id!] = draft.Position;
            }
        }

        const string field = AfterKey + "." + LinkSeriesKey;
        var edges = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var draft in drafts.Where(d => d.After is not null))
        {
            var target = draft.After!.SeriesId;
            if (draft.Id is not null && target == draft.Id)
            {
                errors.Add(new SeriesConfigError(draft.Label, field, "no puede ser la propia serie."));
            }
            else if (!firstPositionById.ContainsKey(target))
            {
                errors.Add(new SeriesConfigError(draft.Label, field, $"la serie '{target}' no existe."));
            }
            else if (draft.Id is not null)
            {
                edges.TryAdd(draft.Id, target);
            }
        }

        ReportCycles(drafts, edges, errors);
    }

    private static void ReportCycles(List<SeriesDraft> drafts, Dictionary<string, string> edges, List<SeriesConfigError> errors)
    {
        // Each series links to at most one other, so following the chain is enough.
        var done = new HashSet<string>(StringComparer.Ordinal);
        foreach (var draft in drafts.Where(d => d.Id is not null && edges.ContainsKey(d.Id)))
        {
            var path = new List<string>();
            var current = draft.Id!;
            while (!done.Contains(current) && !path.Contains(current) && edges.TryGetValue(current, out var next))
            {
                path.Add(current);
                current = next;
            }

            var cycleStart = path.IndexOf(current);
            if (cycleStart >= 0)
            {
                var cycle = path.Skip(cycleStart).Append(current).ToList();
                errors.Add(new SeriesConfigError(
                    SeriesDraft.LabelFor(cycle[0]),
                    AfterKey + "." + LinkSeriesKey,
                    $"ciclo de dependencias {string.Join(" → ", cycle)}."));
            }

            done.UnionWith(path);
        }
    }

    // ---------- YAML helpers ----------

    private static void ReportUnknownKeys(
        YamlMappingNode mapping,
        string[] allowed,
        string? label,
        string? parent,
        List<SeriesConfigError> errors)
    {
        foreach (var key in mapping.Children.Keys)
        {
            var name = (key as YamlScalarNode)?.Value ?? key.ToString();
            if (!allowed.Contains(name, StringComparer.Ordinal))
            {
                var field = parent is null ? name : $"{parent}.{name}";
                errors.Add(new SeriesConfigError(label, field, "campo desconocido."));
            }
        }
    }

    private static bool Has(YamlMappingNode mapping, string key) => mapping.Children.ContainsKey(new YamlScalarNode(key));

    private static YamlNode? Get(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) && !IsNull(node) ? node : null;

    private static bool IsNull(YamlNode node) =>
        node is YamlScalarNode { Style: ScalarStyle.Plain } scalar
        && scalar.Value is null or "" or "~" or "null" or "Null" or "NULL";

    /// <summary>
    /// Reads a text field; reports a type error if it is a list or block. Empty text counts as absent.
    /// </summary>
    private static string? ReadText(
        YamlMappingNode mapping,
        string key,
        string label,
        List<SeriesConfigError> errors,
        string? field = null)
    {
        var node = Get(mapping, key);
        if (node is null)
        {
            return null;
        }

        if (node is not YamlScalarNode scalar)
        {
            errors.Add(new SeriesConfigError(label, field ?? key, "debe ser un texto."));
            return null;
        }

        var value = scalar.Value?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static bool HasError(List<SeriesConfigError> errors, string label, string field) =>
        errors.Exists(e => e.Series == label && e.Field == field);

    private sealed class SeriesDraft(int position)
    {
        public int Position { get; } = position;

        public string? Id { get; set; }

        public string Label => Id is null ? $"Serie n.º {Position}" : LabelFor(Id);

        public string? Name { get; set; }

        public Cadence? Cadence { get; set; }

        public SeriesLink? After { get; set; }

        public static string LabelFor(string id) => $"Serie '{id}'";
    }
}
