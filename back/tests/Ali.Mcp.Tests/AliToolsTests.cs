using System.Text.Json;
using ModelContextProtocol;

namespace Ali.Mcp.Tests;

public class AliToolsTests : IDisposable
{
    private readonly McpTestContent content = new();
    private readonly AliTools tools;

    public AliToolsTests()
    {
        tools = content.Tools();
    }

    public void Dispose() => content.Dispose();

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    // ---------- CA-2: mismos datos que la CLI ----------

    [Fact]
    public void ListPieces_MatchesCliJson()
    {
        Assert.Equal(content.Cli("listar", "--json"), tools.ListPieces());
    }

    [Fact]
    public void ListPieces_WithFilters_MatchesCliJson()
    {
        Assert.Equal(
            content.Cli("listar", "--json", "-s", "casos-reales", "-e", "programado", "-e", "publicado", "--hasta", "2026-11-20"),
            tools.ListPieces(serie: ["casos-reales"], estado: ["programado", "PUBLICADO"], hasta: "2026-11-20"));
    }

    [Fact]
    public void ListPieces_AcceptsToday()
    {
        var pieces = Json(tools.ListPieces(desde: "hoy")).GetProperty("piezas").EnumerateArray().Select(p => p.GetProperty("fichero").GetString());

        Assert.Equal(["casos/01-agentes.md", "casos/02-memoria.md"], pieces);
    }

    [Fact]
    public void FindSlots_MatchesCliJson()
    {
        Assert.Equal(content.Cli("huecos", "casos-reales", "--json", "-n", "3"), tools.FindSlots("casos-reales", cantidad: 3));
        Assert.Equal(content.Cli("huecos", "resumen-caso", "--json", "--desde", "2026-11-01"), tools.FindSlots("resumen-caso", desde: "2026-11-01"));
    }

    [Fact]
    public void ValidateCalendar_MatchesCliJson()
    {
        Assert.Equal(content.Cli("validar", "--json"), tools.ValidateCalendar());
        Assert.Equal(
            content.Cli("validar", "--json", "--desde", "2026-11-01", "--hasta", "2026-12-31", "-s", "resumen-caso"),
            tools.ValidateCalendar(desde: "2026-11-01", hasta: "2026-12-31", serie: ["resumen-caso"]));
    }

    // ---------- CA-3: escritura con las reglas de HU-04 y HU-07 ----------

    [Fact]
    public void ChangeStatus_EditsOnlyTheBlockAndReportsChanges()
    {
        var result = Json(tools.ChangeStatus("casos/02-memoria", estado: "publicado", fecha_publicada: "hoy", url: " https://www.linkedin.com/pulse/y "));

        Assert.Equal("casos/02-memoria.md", result.GetProperty("fichero").GetString());
        var changes = result.GetProperty("cambios").EnumerateArray().ToList();
        Assert.Equal(["estado", "fecha_publicada", "url"], changes.Select(c => c.GetProperty("campo").GetString()));
        Assert.Equal("programado", changes[0].GetProperty("antes").GetString());
        Assert.Equal("publicado", changes[0].GetProperty("despues").GetString());
        Assert.Equal(JsonValueKind.Null, changes[1].GetProperty("antes").ValueKind);
        Assert.Equal("2026-11-15", changes[1].GetProperty("despues").GetString());

        Assert.Equal(
            "---\nserie: casos-reales\nestado: publicado\nfecha_prevista: 2026-11-24\nfecha_publicada: 2026-11-15\nurl: https://www.linkedin.com/pulse/y\n---\n# Memoria\n",
            content.ReadText("casos/02-memoria.md"));
    }

    [Fact]
    public void ChangeStatus_SameValues_ReturnsNoChanges()
    {
        var result = Json(tools.ChangeStatus("casos/02-memoria.md", estado: "programado"));

        Assert.Empty(result.GetProperty("cambios").EnumerateArray());
    }

    [Fact]
    public void CreatePiece_CreatesFileAndReturnsMetadata()
    {
        var result = Json(tools.CreatePiece("casos-reales", "Qué aprendimos del RAG", fecha_libre: true, formato: "articulo"));

        Assert.Equal("casos/03-que-aprendimos-del-rag.md", result.GetProperty("fichero").GetString());
        Assert.Equal("casos-reales", result.GetProperty("serie").GetString());
        Assert.Equal("idea", result.GetProperty("estado").GetString());
        Assert.Equal("2026-12-01", result.GetProperty("fecha_prevista").GetString());
        Assert.Equal("articulo", result.GetProperty("formato").GetString());
        Assert.Empty(result.GetProperty("depende_de").EnumerateArray());
        Assert.StartsWith("---\nserie: casos-reales\nestado: idea\nfecha_prevista: 2026-12-01\n", content.ReadText("casos/03-que-aprendimos-del-rag.md"));
    }

    // ---------- HU-11: hora y renombrado ----------

    [Fact]
    public void ChangeStatus_Time_AppearsInChangesWithoutRename()
    {
        var result = Json(tools.ChangeStatus("casos/02-memoria", hora: "16:00"));

        var change = Assert.Single(result.GetProperty("cambios").EnumerateArray());
        Assert.Equal("hora", change.GetProperty("campo").GetString());
        Assert.Equal("16:00", change.GetProperty("despues").GetString());
        Assert.Empty(result.GetProperty("renombrado").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("aviso").ValueKind);
        Assert.Contains("\"hora\": \"16:00\"", tools.ListPieces(serie: ["casos-reales"]));
    }

    [Fact]
    public void CreateAndMove_WithDateInName_RenamesTheFile()
    {
        // The source file may use \r\n or \n; YAML accepts a mix.
        File.WriteAllText(content.SeriesFile, McpTestContent.SeriesYaml.Replace(
            "carpeta: casos",
            "carpeta: casos\n    fichero: \"{nn} - {aammdd} - {titulo_original}\"",
            StringComparison.Ordinal));

        var created = Json(tools.CreatePiece("casos-reales", "¿Evaluar agentes?", fecha: "2026-12-01", hora: "16:00"));
        Assert.Equal("casos/03 - 261201 - Evaluar agentes.md", created.GetProperty("fichero").GetString());
        Assert.Equal("16:00", created.GetProperty("hora").GetString());

        var moved = Json(tools.ChangeStatus("casos/03 - 261201 - Evaluar agentes", fecha_prevista: "2026-12-08"));

        Assert.Equal("casos/03 - 261208 - Evaluar agentes.md", moved.GetProperty("fichero").GetString());
        var renamed = Assert.Single(moved.GetProperty("renombrado").EnumerateArray());
        Assert.Equal("casos/03 - 261201 - Evaluar agentes.md", renamed.GetProperty("antes").GetString());
        Assert.True(File.Exists(Path.Combine(content.Root, "casos", "03 - 261208 - Evaluar agentes.md")));
    }

    [Fact]
    public void InvalidTime_ThrowsMcpException()
    {
        Assert.Equal(
            "Hora '25:00' no válida en hora. Formato esperado: HH:mm.",
            Assert.Throws<McpException>(() => tools.ChangeStatus("casos/02-memoria", hora: "25:00")).Message);
    }

    [Fact]
    public void CreatePiece_AfterSeries_AddsDependency()
    {
        var result = Json(tools.CreatePiece("resumen-caso", "Resumen memoria", fecha_libre: true));

        Assert.Equal("2026-11-26", result.GetProperty("fecha_prevista").GetString());
        Assert.Equal(["casos/02-memoria"], result.GetProperty("depende_de").EnumerateArray().Select(d => d.GetString()));
    }

    // ---------- CA-4: errores ----------

    [Fact]
    public void ChangeStatus_InvalidRequest_ThrowsMcpExceptionAndLeavesFile()
    {
        var before = content.ReadText("casos/02-memoria.md");

        var ex = Assert.Throws<McpException>(() => tools.ChangeStatus("casos/02-memoria", estado: "publicado"));

        Assert.Equal("Para marcar como publicado hace falta --fecha-publicada (la pieza no tiene fecha_publicada).", ex.Message);
        Assert.Equal(before, content.ReadText("casos/02-memoria.md"));
    }

    [Fact]
    public void CreatePiece_ExistingFile_IsNotOverwritten()
    {
        content.Folder.Write("casos/03-repetida.md", "previo");

        var ex = Assert.Throws<McpException>(() => tools.CreatePiece("casos-reales", "Repetida"));

        Assert.Equal("Ya existe 'casos/03-repetida.md'; no se sobrescribe.", ex.Message);
        Assert.Equal("previo", content.ReadText("casos/03-repetida.md"));
    }

    [Theory]
    [InlineData("mañana", "Fecha 'mañana' no válida en desde. Formato esperado: AAAA-MM-DD u hoy.")]
    [InlineData("17/11/2026", "Fecha '17/11/2026' no válida en desde. Formato esperado: AAAA-MM-DD u hoy.")]
    public void InvalidDate_UsesParameterName(string value, string message)
    {
        Assert.Equal(message, Assert.Throws<McpException>(() => tools.FindSlots("casos-reales", desde: value)).Message);
        Assert.Equal(message, Assert.Throws<McpException>(() => tools.ValidateCalendar(desde: value)).Message);
    }

    [Fact]
    public void InvalidStatusAndRange_ThrowMcpException()
    {
        Assert.Equal(
            "Estado 'hecho' no válido. Valores admitidos: idea, borrador, listo, programado, publicado.",
            Assert.Throws<McpException>(() => tools.ListPieces(estado: ["hecho"])).Message);
        Assert.Equal(
            "La fecha 'desde' (2026-12-01) es posterior a 'hasta' (2026-11-01).",
            Assert.Throws<McpException>(() => tools.ListPieces(desde: "2026-12-01", hasta: "2026-11-01")).Message);
        Assert.Equal(
            "La cantidad debe ser un entero entre 1 y 100.",
            Assert.Throws<McpException>(() => tools.FindSlots("casos-reales", cantidad: 0)).Message);
    }

    [Fact]
    public void MissingSeriesFile_OnlyAffectsToolsThatNeedIt()
    {
        File.Delete(content.SeriesFile);

        Assert.Contains("casos/01-agentes.md", tools.ListPieces());
        Assert.StartsWith("El fichero de series", Assert.Throws<McpException>(() => tools.FindSlots("casos-reales")).Message);
        Assert.StartsWith("El fichero de series", Assert.Throws<McpException>(() => tools.ValidateCalendar()).Message);
        Assert.StartsWith("El fichero de series", Assert.Throws<McpException>(() => tools.CreatePiece("casos-reales", "X")).Message);
    }

    // ---------- CA-6: datos siempre actuales ----------

    [Fact]
    public void EachCall_SeesManualEdits()
    {
        Assert.DoesNotContain("casos/09-nueva.md", tools.ListPieces());

        content.Folder.Write("casos/09-nueva.md", "---\nserie: casos-reales\nestado: listo\nfecha_prevista: 2026-12-01\n---\n");
        File.WriteAllText(content.SeriesFile, McpTestContent.SeriesYaml.Replace("dias: martes", "dias: [martes, jueves]"));

        Assert.Contains("casos/09-nueva.md", tools.ListPieces());
        var slots = Json(tools.FindSlots("casos-reales", cantidad: 2)).GetProperty("huecos").EnumerateArray().Select(s => s.GetProperty("fecha").GetString());
        Assert.Equal(["2026-11-19", "2026-11-26"], slots);
    }
}
