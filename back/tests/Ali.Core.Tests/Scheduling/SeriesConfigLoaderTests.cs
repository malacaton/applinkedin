using System.Text;
using Ali.Core.Scheduling;
using Ali.Core.Tests.Content;

namespace Ali.Core.Tests.Scheduling;

public class SeriesConfigLoaderTests : IDisposable
{
    private const string SpecExample = """
        series:
          - id: casos-reales
            nombre: Casos reales
            cadencia:
              dias: [martes]
              frecuencia: semanal
              hora: "09:00"

          - id: fundamentos
            nombre: Fundamentos
            cadencia:
              dias: [lunes, jueves]
              frecuencia: quincenal
              desde: 2026-11-02          # semana de referencia para la alternancia
              hora: "08:30"

          - id: resumen-caso
            nombre: Resumen de un caso
            despues_de:
              serie: casos-reales
              dias: 2                    # dos días después de su pieza relacionada

          - id: notas
            nombre: Notas sueltas        # sin cadencia: publicación libre
        """;

    private readonly TempContentFolder folder = new();
    private readonly SeriesConfigLoader loader = new();

    public void Dispose() => folder.Dispose();

    private SeriesConfig Load(string yaml) => loader.Load(folder.Write("series.yaml", yaml));

    private IReadOnlyList<SeriesConfigError> Errors(string yaml) =>
        Assert.Throws<SeriesConfigException>(() => Load(yaml)).Errors;

    private static string Series(string body) => "series:\n" + body;

    // ---------- CA-1: ejemplo completo ----------

    [Fact]
    public void Load_SpecExample_ReadsEveryField()
    {
        var config = Load(SpecExample);

        Assert.Equal(["casos-reales", "fundamentos", "resumen-caso", "notas"], config.Series.Select(s => s.Id));

        var casos = config.Find("casos-reales")!;
        Assert.Equal("Casos reales", casos.Name);
        Assert.Equal([DayOfWeek.Tuesday], casos.Cadence!.Days);
        Assert.Equal(CadenceFrequency.Weekly, casos.Cadence.Frequency);
        Assert.Null(casos.Cadence.Anchor);
        Assert.Equal(new TimeOnly(9, 0), casos.Cadence.Time);
        Assert.Null(casos.After);

        var fundamentos = config.Find("fundamentos")!;
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Thursday], fundamentos.Cadence!.Days);
        Assert.Equal(CadenceFrequency.Biweekly, fundamentos.Cadence.Frequency);
        Assert.Equal(new DateOnly(2026, 11, 2), fundamentos.Cadence.Anchor);
        Assert.Equal(new TimeOnly(8, 30), fundamentos.Cadence.Time);

        var resumen = config.Find("resumen-caso")!;
        Assert.Null(resumen.Cadence);
        Assert.Equal(new SeriesLink("casos-reales", 2), resumen.After);

        Assert.Null(config.Find("no-existe"));
    }

    [Fact]
    public void Load_EmptySeriesList_IsValid()
    {
        Assert.Empty(Load("series: []").Series);
        Assert.Empty(Load("series:").Series);
    }

    // ---------- CA-2: sin cadencia ----------

    [Fact]
    public void Load_SeriesWithoutCadence_HasNullCadence()
    {
        var series = Assert.Single(Load(Series("  - id: notas\n    nombre: Notas\n")).Series);

        Assert.Null(series.Cadence);
        Assert.Null(series.After);
    }

    [Fact]
    public void Load_SeriesWithCadenceAndLink_KeepsBoth()
    {
        var config = Load(Series("""
              - id: a
                nombre: A
                cadencia: { dias: martes, frecuencia: semanal }
              - id: b
                nombre: B
                cadencia: { dias: jueves, frecuencia: semanal }
                despues_de: { serie: a, dias: 2 }
            """));

        var b = config.Find("b")!;
        Assert.NotNull(b.Cadence);
        Assert.Equal(new SeriesLink("a", 2), b.After);
    }

    // ---------- CA-3: despues_de ----------

    [Theory]
    [InlineData("otra", "la serie 'otra' no existe.")]
    [InlineData("b", "no puede ser la propia serie.")]
    public void Load_LinkToMissingOrSelf_Fails(string target, string reason)
    {
        var errors = Errors(Series($"""
              - id: a
                nombre: A
              - id: b
                nombre: B
                despues_de: {"{"} serie: {target}, dias: 2 {"}"}
            """));

        var error = Assert.Single(errors);
        Assert.Equal("Serie 'b'", error.Series);
        Assert.Equal("despues_de.serie", error.Field);
        Assert.Equal(reason, error.Reason);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("61")]
    [InlineData("-1")]
    [InlineData("dos")]
    [InlineData("1.5")]
    public void Load_LinkDaysOutOfRange_Fails(string days)
    {
        var errors = Errors(Series($"""
              - id: a
                nombre: A
              - id: b
                nombre: B
                despues_de: {"{"} serie: a, dias: {days} {"}"}
            """));

        var error = Assert.Single(errors);
        Assert.Equal("despues_de.dias", error.Field);
        Assert.Equal("debe ser un entero entre 1 y 60.", error.Reason);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("60")]
    public void Load_LinkDaysAtLimits_IsValid(string days)
    {
        var config = Load(Series($"""
              - id: a
                nombre: A
              - id: b
                nombre: B
                despues_de: {"{"} serie: a, dias: {days} {"}"}
            """));

        Assert.Equal(int.Parse(days), config.Find("b")!.After!.Days);
    }

    [Fact]
    public void Load_LinkCycle_ReportsCycleOnce()
    {
        var errors = Errors(Series("""
              - id: a
                nombre: A
                despues_de: { serie: b, dias: 1 }
              - id: b
                nombre: B
                despues_de: { serie: c, dias: 1 }
              - id: c
                nombre: C
                despues_de: { serie: a, dias: 1 }
              - id: d
                nombre: D
                despues_de: { serie: a, dias: 1 }
            """));

        var error = Assert.Single(errors);
        Assert.Equal("Serie 'a'", error.Series);
        Assert.Equal("despues_de.serie", error.Field);
        Assert.Equal("ciclo de dependencias a → b → c → a.", error.Reason);
    }

    [Fact]
    public void Load_LinkChainWithoutCycle_IsValid()
    {
        var config = Load(Series("""
              - id: a
                nombre: A
              - id: b
                nombre: B
                despues_de: { serie: a, dias: 1 }
              - id: c
                nombre: C
                despues_de: { serie: b, dias: 3 }
            """));

        Assert.Equal(3, config.Series.Count);
    }

    // ---------- CA-4: un error por caso ----------

    [Fact]
    public void Load_MissingFile_Fails()
    {
        var ex = Assert.Throws<SeriesConfigException>(() => loader.Load(Path.Combine(folder.Root, "no-existe.yaml")));

        Assert.Contains("No existe el fichero de series", Assert.Single(ex.Errors).Reason);
    }

    [Fact]
    public void Load_BlankPath_Fails()
    {
        Assert.Throws<SeriesConfigException>(() => loader.Load("  "));
    }

    [Fact]
    public void Load_InvalidYaml_ReportsLine()
    {
        var error = Assert.Single(Errors("series:\n  - id: a\n    nombre: [sin cerrar\n"));

        Assert.Null(error.Series);
        Assert.StartsWith("YAML no válido en la línea", error.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("otra_clave: 1")]
    [InlineData("series: casos")]
    [InlineData("- id: a")]
    public void Load_WithoutSeriesList_Fails(string yaml)
    {
        var errors = Errors(yaml);

        Assert.Contains(errors, e => e.Reason == "El fichero debe tener una clave 'series' con la lista de series.");
    }

    [Theory]
    [InlineData("  - nombre: Sin id\n", "Serie n.º 1", "id", "es obligatorio.")]
    [InlineData("  - id: a\n", "Serie 'a'", "nombre", "es obligatorio.")]
    [InlineData("  - id: a\n    nombre: ~\n", "Serie 'a'", "nombre", "es obligatorio.")]
    [InlineData("  - id: Casos Reales\n    nombre: X\n", "Serie n.º 1", "id", "'Casos Reales' no es válido; usa minúsculas, dígitos y guiones.")]
    [InlineData("  - id: casos_reales\n    nombre: X\n", "Serie n.º 1", "id", "'casos_reales' no es válido; usa minúsculas, dígitos y guiones.")]
    [InlineData("  - id: a\n    nombre: [X]\n", "Serie 'a'", "nombre", "debe ser un texto.")]
    [InlineData("  - id: a\n    nombre: X\n    color: rojo\n", "Serie 'a'", "color", "campo desconocido.")]
    [InlineData("  - solo texto\n", "Serie n.º 1", null, "debe ser un bloque de pares clave: valor.")]
    public void Load_InvalidSeriesField_ReportsSeriesFieldAndReason(string body, string series, string? field, string reason)
    {
        var error = Assert.Single(Errors(Series(body)));

        Assert.Equal(series, error.Series);
        Assert.Equal(field, error.Field);
        Assert.Equal(reason, error.Reason);
    }

    [Fact]
    public void Load_DuplicateId_ReportsBothPositions()
    {
        var error = Assert.Single(Errors(Series("""
              - id: notas
                nombre: A
              - id: b
                nombre: B
              - id: notas
                nombre: C
            """)));

        Assert.Equal("Serie 'notas'", error.Series);
        Assert.Equal("id", error.Field);
        Assert.Equal("está repetido (series n.º 1 y n.º 3).", error.Reason);
    }

    [Theory]
    [InlineData("dias: [juebes]\n      frecuencia: semanal", "cadencia.dias", "'juebes' no es un día de la semana.")]
    [InlineData("dias: [martes, Martes]\n      frecuencia: semanal", "cadencia.dias", "'Martes' está repetido.")]
    [InlineData("dias: []\n      frecuencia: semanal", "cadencia.dias", "debe tener al menos un día.")]
    [InlineData("frecuencia: semanal", "cadencia.dias", "es obligatorio.")]
    [InlineData("dias: [martes]", "cadencia.frecuencia", "es obligatorio.")]
    [InlineData("dias: [martes]\n      frecuencia: mensual", "cadencia.frecuencia", "'mensual' no es válida; usa semanal o quincenal.")]
    [InlineData("dias: [martes]\n      frecuencia: quincenal", "cadencia.desde", "es obligatorio con frecuencia quincenal.")]
    [InlineData("dias: [martes]\n      frecuencia: quincenal\n      desde: 02/11/2026", "cadencia.desde", "'02/11/2026' no es una fecha AAAA-MM-DD válida.")]
    [InlineData("dias: [martes]\n      frecuencia: semanal\n      hora: \"25:00\"", "cadencia.hora", "'25:00' no es una hora HH:mm válida.")]
    [InlineData("dias: [martes]\n      frecuencia: semanal\n      hora: mañana", "cadencia.hora", "'mañana' no es una hora HH:mm válida.")]
    [InlineData("dias: [martes]\n      frecuencia: semanal\n      frequencia: semanal", "cadencia.frequencia", "campo desconocido.")]
    public void Load_InvalidCadence_ReportsField(string cadence, string field, string reason)
    {
        var errors = Errors(Series($"  - id: x\n    nombre: X\n    cadencia:\n      {cadence}\n"));

        var error = Assert.Single(errors);
        Assert.Equal("Serie 'x'", error.Series);
        Assert.Equal(field, error.Field);
        Assert.Equal(reason, error.Reason);
    }

    [Fact]
    public void Load_CadenceThatIsNotABlock_Fails()
    {
        var error = Assert.Single(Errors(Series("  - id: x\n    nombre: X\n    cadencia: semanal\n")));

        Assert.Equal("cadencia", error.Field);
    }

    [Fact]
    public void Load_LinkMissingFields_ReportsEach()
    {
        var errors = Errors(Series("""
              - id: a
                nombre: A
                despues_de: { otra: 1 }
            """));

        Assert.Equal(
            ["despues_de.otra", "despues_de.serie", "despues_de.dias"],
            errors.Select(e => e.Field));
    }

    [Fact]
    public void Exception_Message_ListsEveryErrorInSpanish()
    {
        var ex = Assert.Throws<SeriesConfigException>(() => Load(Series("  - id: a\n")));

        Assert.Contains("tiene 1 error:", ex.Message);
        Assert.Contains("- Serie 'a', campo 'nombre': es obligatorio.", ex.Message);
    }

    // ---------- CA-5: varios errores a la vez ----------

    [Fact]
    public void Load_SeveralErrors_ReportsAll()
    {
        var ex = Assert.Throws<SeriesConfigException>(() => Load(Series("""
              - id: a
              - id: b
                nombre: B
                cadencia: { dias: [juebes], frecuencia: semanal }
              - id: c
                nombre: C
                despues_de: { serie: z, dias: 2 }
            """)));

        Assert.Equal(
            [
                ("Serie 'a'", "nombre"),
                ("Serie 'b'", "cadencia.dias"),
                ("Serie 'c'", "despues_de.serie"),
            ],
            ex.Errors.Select(e => (e.Series!, e.Field!)));
        Assert.Contains("tiene 3 errores:", ex.Message);
    }

    // ---------- CA-6: Matches ----------

    [Theory]
    [InlineData("2026-11-17", true)]   // martes
    [InlineData("2026-11-24", true)]   // martes
    [InlineData("2026-11-18", false)]  // miércoles
    [InlineData("2026-11-15", false)]  // domingo
    public void Matches_Weekly(string date, bool expected)
    {
        var cadence = new Cadence([DayOfWeek.Tuesday], CadenceFrequency.Weekly, null, null);

        Assert.Equal(expected, cadence.Matches(DateOnly.Parse(date)));
    }

    [Theory]
    [InlineData("2026-11-02", true)]   // lunes de la semana ancla
    [InlineData("2026-11-05", true)]   // jueves de la semana ancla
    [InlineData("2026-11-09", false)]  // lunes, semana impar
    [InlineData("2026-11-12", false)]  // jueves, semana impar
    [InlineData("2026-11-16", true)]   // lunes, dos semanas después
    [InlineData("2026-11-04", false)]  // miércoles de la semana ancla: no es día de publicación
    [InlineData("2026-10-19", true)]   // dos semanas antes del ancla
    [InlineData("2026-10-26", false)]  // una semana antes del ancla
    [InlineData("2026-12-28", true)]   // cambio de año: 8 semanas después
    [InlineData("2027-01-04", false)]  // 9 semanas después
    [InlineData("2027-01-11", true)]   // 10 semanas después
    public void Matches_Biweekly(string date, bool expected)
    {
        // Anchor on a Wednesday: the whole Monday-Sunday week is a publication week.
        var cadence = new Cadence(
            [DayOfWeek.Monday, DayOfWeek.Thursday],
            CadenceFrequency.Biweekly,
            new DateOnly(2026, 11, 4),
            null);

        Assert.Equal(expected, cadence.Matches(DateOnly.Parse(date)));
    }

    [Fact]
    public void Matches_BiweeklyAnchorOnSunday_UsesThatMondayToSundayWeek()
    {
        // 2026-11-08 is a Sunday: its week starts on Monday 2026-11-02.
        var cadence = new Cadence([DayOfWeek.Monday], CadenceFrequency.Biweekly, new DateOnly(2026, 11, 8), null);

        Assert.True(cadence.Matches(new DateOnly(2026, 11, 2)));
        Assert.False(cadence.Matches(new DateOnly(2026, 11, 9)));
    }

    // ---------- CA-7: días ----------

    [Fact]
    public void Load_DayNames_AcceptAccentsCaseAndSingleValue()
    {
        var config = Load(Series("""
              - id: a
                nombre: A
                cadencia: { dias: [MIÉRCOLES, sabado, Domingo], frecuencia: Semanal }
              - id: b
                nombre: B
                cadencia: { dias: miércoles, frecuencia: semanal }
            """));

        Assert.Equal(
            [DayOfWeek.Wednesday, DayOfWeek.Saturday, DayOfWeek.Sunday],
            config.Find("a")!.Cadence!.Days);
        Assert.Equal([DayOfWeek.Wednesday], config.Find("b")!.Cadence!.Days);
    }

    // ---------- CA-8: solo lectura y codificación ----------

    [Fact]
    public void Load_DoesNotModifyFile_AndAcceptsBomAndCrlf()
    {
        var text = SpecExample.Replace("\n", "\r\n");
        var path = folder.WriteBytes("series.yaml", [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)]);
        var bytesBefore = File.ReadAllBytes(path);
        var modifiedBefore = File.GetLastWriteTimeUtc(path);

        var config = loader.Load(path);

        Assert.Equal(4, config.Series.Count);
        Assert.Equal(bytesBefore, File.ReadAllBytes(path));
        Assert.Equal(modifiedBefore, File.GetLastWriteTimeUtc(path));
    }
}
