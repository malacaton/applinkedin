using System.Reflection;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using YamlDotNet.RepresentationModel;

namespace Ali.Mcp.Tests;

/// <summary>
/// HU-10: the example skill is well formed and consistent with the real tools of the server.
/// </summary>
public partial class ExampleSkillTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string SkillFolder = Path.Combine(RepositoryRoot, "skill", "planificar-publicaciones");
    private static readonly string Skill = File.ReadAllText(Path.Combine(SkillFolder, "SKILL.md")).Replace("\r\n", "\n");

    /// <summary>
    /// Tool name → parameter names, read from the attributes of <see cref="AliTools"/>.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> Tools = typeof(AliTools)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .Select(m => (Method: m, Attribute: m.GetCustomAttribute<McpServerToolAttribute>()))
        .Where(x => x.Attribute is not null)
        .ToDictionary(x => x.Attribute!.Name!, x => x.Method.GetParameters().Select(p => p.Name!).ToHashSet());

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SkillNamePattern();

    // Backquoted snake_case identifiers: tool and parameter names.
    [GeneratedRegex("`([a-z]+(?:_[a-z]+)+)`")]
    private static partial Regex SnakeCaseCode();

    [GeneratedRegex("`([a-z_]+)`")]
    private static partial Regex Code();

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) && Directory.Exists(Path.Combine(directory.FullName, "skill")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    // ---------- CA-1: formato ----------

    [Fact]
    public void Skill_HasValidFrontMatter()
    {
        Assert.StartsWith("---\n", Skill);
        var end = Skill.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        Assert.True(end > 0, "El bloque YAML de SKILL.md no está cerrado.");

        var yaml = new YamlStream();
        yaml.Load(new StringReader(Skill[4..end]));
        var mapping = Assert.IsType<YamlMappingNode>(yaml.Documents[0].RootNode);
        var name = ((YamlScalarNode)mapping[new YamlScalarNode("name")]).Value!;
        var description = ((YamlScalarNode)mapping[new YamlScalarNode("description")]).Value!;

        Assert.Equal("planificar-publicaciones", name);
        Assert.Matches(SkillNamePattern(), name);
        Assert.True(name.Length <= 64);
        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.True(description.Length <= 1024, $"description tiene {description.Length} caracteres.");
    }

    // ---------- CA-2: herramientas ----------

    [Fact]
    public void Server_HasTheFiveToolsTheSkillDescribes()
    {
        Assert.Equal(
            ["buscar_huecos", "cambiar_estado", "crear_pieza", "listar_piezas", "validar_calendario"],
            Tools.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Skill_MentionsEveryTool()
    {
        foreach (var tool in Tools.Keys)
        {
            Assert.Contains($"`{tool}`", Skill);
        }
    }

    [Fact]
    public void Skill_OnlyMentionsExistingToolsAndParameters()
    {
        var known = Tools.Keys.Concat(Tools.Values.SelectMany(p => p)).ToHashSet();

        var unknown = SnakeCaseCode().Matches(Skill).Select(m => m.Groups[1].Value).Where(v => !known.Contains(v)).Distinct().ToList();

        Assert.Empty(unknown);
    }

    [Fact]
    public void Skill_ToolTable_CitesOnlyParametersOfEachTool()
    {
        var rows = Skill.Split('\n').Where(l => l.StartsWith("| `", StringComparison.Ordinal)).ToList();
        Assert.Equal(5, rows.Count);

        foreach (var row in rows)
        {
            var codes = Code().Matches(row).Select(m => m.Groups[1].Value).ToList();
            var tool = codes[0];
            Assert.True(Tools.ContainsKey(tool), $"La tabla cita una herramienta que no existe: {tool}");

            var foreign = codes.Skip(1).Where(c => !Tools[tool].Contains(c)).ToList();
            Assert.True(foreign.Count == 0, $"{tool}: la tabla cita parámetros que no tiene: {string.Join(", ", foreign)}");
        }
    }

    // ---------- Enlaces y manual ----------

    [Fact]
    public void Skill_LinksEditorialCriteria_AndTheFileExists()
    {
        Assert.Contains("(criterios-editoriales.md)", Skill);
        var criteria = File.ReadAllText(Path.Combine(SkillFolder, "criterios-editoriales.md"));
        Assert.Contains("personalizables", criteria);
    }

    [Fact]
    public void Manual_ExplainsTheSkill()
    {
        var manual = File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "manual.md"));

        Assert.Contains("skill/planificar-publicaciones", manual);
        Assert.Contains("criterios-editoriales.md", manual);
        Assert.Contains("~/.claude/skills/", manual);
    }
}
