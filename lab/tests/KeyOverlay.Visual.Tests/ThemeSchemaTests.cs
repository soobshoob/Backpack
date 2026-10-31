using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Tests;

/// <summary>
/// Воспроизводимая проверка схемы темы и примеров полноценным валидатором JSON Schema 2020-12 (JsonSchema.Net),
/// вместо временного скрипта Phase 2A.
/// </summary>
public class ThemeSchemaTests
{
    private static readonly JsonSchema Schema = JsonSchema.FromFile(Repo.PathOf("docs", "themes", "keyoverlay-theme.schema.json"));

    private static EvaluationResults Evaluate(JsonNode node) =>
        Schema.Evaluate(JsonDocument.Parse(node.ToJsonString()).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

    private static JsonNode Load(string file) => JsonNode.Parse(File.ReadAllText(file))!;

    public static TheoryData<string> Examples()
    {
        var data = new TheoryData<string>();
        foreach (var f in Directory.GetFiles(Repo.PathOf("docs", "themes", "examples"), "*.json"))
        {
            data.Add(Path.GetFileName(f));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Example_IsValid(string file)
    {
        var r = Evaluate(Load(Repo.PathOf("docs", "themes", "examples", file)));
        Assert.True(r.IsValid, string.Join("; ", r.Details?.Where(d => d.Errors is not null).SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}")) ?? []));
    }

    [Fact]
    public void AtLeastTwoExamples()
    {
        Assert.True(Examples().Count >= 2);
    }

    [Theory]
    [InlineData("top", "monitor", "\"DISPLAY1\"")]
    [InlineData("keycap", "script", "\"x\"")]
    [InlineData("colors", "primary", "\"FFFFFF\"")]
    [InlineData("text", "color", "\"red\"")]
    [InlineData("border", "color", "\"#FFFFFFF\"")]
    [InlineData("border", "radius", "1.5")]
    [InlineData("keycap", "size", "4")]
    [InlineData("animation", "durationMs", "250.5")]
    [InlineData("top", "formatVersion", "2")]
    [InlineData("keycap", "style", "\"custom-xaml\"")]
    [InlineData("top", "id", "\"My.Theme\"")]
    [InlineData("layout", "showIcon", "\"true\"")]
    [InlineData("top", "format", "\"keyviz\"")]
    public void Mutation_IsRejected(string section, string field, string valueJson)
    {
        var doc = Load(Repo.PathOf("docs", "themes", "examples", "keyoverlay-default.json"));
        var target = section == "top" ? doc : doc[section]!;
        target[field] = JsonNode.Parse(valueJson);
        Assert.False(Evaluate(doc).IsValid);
    }

    [Theory]
    [InlineData("keycap")]
    [InlineData("format")]
    [InlineData("id")]
    public void MissingRequired_IsRejected(string field)
    {
        var doc = (JsonObject)Load(Repo.PathOf("docs", "themes", "examples", "keyoverlay-default.json"));
        doc.Remove(field);
        Assert.False(Evaluate(doc).IsValid);
    }

    [Fact]
    public void EmptyFontList_IsRejected()
    {
        var doc = Load(Repo.PathOf("docs", "themes", "examples", "keyoverlay-default.json"));
        doc["text"]!["font"]!["families"] = new JsonArray();
        Assert.False(Evaluate(doc).IsValid);
    }

    /// <summary>Внешний вид из настроек стенда — это корректные разделы темы: второго формата цветов и единиц нет.</summary>
    [Fact]
    public void SettingsAppearance_IsValidThemeContent()
    {
        foreach (var a in new[]
        {
            AppearanceSettings.Default,
            AppearanceSettings.Default with { Size = 128, Radius = 0, BorderWidth = 16, Animation = AnimationKind.None, DurationMs = 50, BackgroundColor = ThemeColor.Parse("#00000000") },
        })
        {
            var theme = SettingsSerializer.AppearanceToJson(a);
            theme["format"] = "keyoverlay-theme";
            theme["formatVersion"] = 1;
            theme["id"] = "lab.settings";
            theme["name"] = "Настройки стенда";
            Assert.True(Evaluate(theme).IsValid);
        }
    }

    internal static readonly JsonSchema SettingsSchema = JsonSchema.FromFile(Repo.PathOf("docs", "settings", "keyoverlay-lab-settings.schema.json"));

    /// <summary>Файл настроек, который пишет программа, валиден по своей схеме, а его раздел appearance — по схеме темы.</summary>
    [Fact]
    public void WrittenSettings_AreValid_AgainstSettingsAndThemeSchemas()
    {
        var variants = new[]
        {
            LabSettings.Default,
            LabSettings.Default with
            {
                Appearance = AppearanceSettings.Default with { LabelSet = KeyOverlay.Visual.Rendering.LabelSet.MacOS, LabelMode = KeyOverlay.Visual.Rendering.LabelMode.Text, PressAnimation = false, Indicator = IndicatorVisual.Default with { BorderWidth = 0, Radius = 1 } },
                Placement = new PlacementSettings(ScreenAnchor.Center, 0, 400, -2000, 2000),
                Keys = new KeysSettings(KeyFilter.CombosOnly, TrailMode.FadeInPlace, 60_000, false, MouseLabelMode.Text),
                LayoutIndicator = new LayoutIndicatorSettings(true, 5000, false, true, IndicatorAnimation.Fade, IndicatorPosition.Screen, ScreenAnchor.BottomLeft, 10, -10),
                MouseCursor = new MouseCursorSettings(true, true, false, -200, 200),
            },
        };
        foreach (var s in variants)
        {
            var json = SettingsSerializer.ToJson(s);
            var r = SettingsSchema.Evaluate(JsonDocument.Parse(json.ToJsonString()).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            Assert.True(r.IsValid, Errors(r));
            var theme = SettingsSerializer.AppearanceToJson(s.Appearance);
            theme["format"] = "keyoverlay-theme";
            theme["formatVersion"] = 1;
            theme["id"] = "lab.settings";
            theme["name"] = "Настройки стенда";
            Assert.True(Evaluate(theme).IsValid, Errors(Evaluate(theme)));
        }
    }

    [Theory]
    [InlineData("keys", "filter", "\"letters\"")]
    [InlineData("placement", "offsetX", "5000")]
    [InlineData("placement", "marginX", "10")]
    [InlineData("mouseCursor", "cursorX", "100")]
    [InlineData("top", "formatVersion", "1")]
    public void SettingsSchema_RejectsInvalid(string section, string field, string valueJson)
    {
        var json = SettingsSerializer.ToJson(LabSettings.Default);
        var target = section == "top" ? json : json[section]!;
        target[field] = JsonNode.Parse(valueJson);
        Assert.False(SettingsSchema.Evaluate(JsonDocument.Parse(json.ToJsonString()).RootElement).IsValid);
    }

    private static string Errors(EvaluationResults r) =>
        string.Join("; ", r.Details?.Where(d => d.Errors is not null).SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}")) ?? []);

    [Fact]
    public void HelpJsonBlocks_AreValidThemes()
    {
        int checkedBlocks = 0;
        // Примеры тем — в документации для разработчиков (docs/themes/dev); справка описывает только работающее.
        foreach (var md in Directory.GetFiles(Repo.PathOf("docs", "help"), "*.md", SearchOption.AllDirectories).Concat(Directory.GetFiles(Repo.PathOf("docs", "themes", "dev"), "*.md")))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(md), "```json\\r?\\n(.*?)```", RegexOptions.Singleline))
            {
                var node = JsonNode.Parse(m.Groups[1].Value)!;
                if ((string?)node["format"] == "keyoverlay-theme")
                {
                    Assert.True(Evaluate(node).IsValid, Path.GetFileName(md));
                    checkedBlocks++;
                }
            }
        }

        Assert.True(checkedBlocks >= 2);
    }
}
