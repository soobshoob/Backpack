using System.IO;
using System.Text.Json.Nodes;
using Backpack.Sdk;
using Backpack.Visual.Plugins;
using Backpack.Visual.Presentation;

namespace Backpack.Visual.Tests;

/// <summary>
/// Рамка по умолчанию «Фокуса», «Линзы» и «Снимка поверх окон» — одна константа <see cref="ToolDefaults.BorderColor"/> (#31C99A).
/// Чистая установка получает новый цвет; цвет, выбранный пользователем, сохраняется; прежнее значение по умолчанию (#D4D4D4),
/// которое прежние версии сохраняли вместе со всеми полями страницы, заменяется; остальные настройки не трогаются.
/// </summary>
public sealed class BorderDefaultsTests : IDisposable
{
    private static readonly string[] Plugins = ["Focus", "Lens", "SnapshotOverlay"];
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bp-border-" + Guid.NewGuid().ToString("N")[..8]);

    public BorderDefaultsTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    private static PluginManifest Sample(string name)
    {
        var (m, errors) = PluginManifest.Parse(File.ReadAllText(Repo.PathOf("lab", "samples", name, "manifest.json")));
        Assert.True(m is not null, string.Join("; ", errors));
        return m!;
    }

    private static string Border(JsonObject values) => (string)values["borderColor"]!;

    [Fact]
    public void Shared_constant_is_the_backpack_emerald_and_every_default_uses_it()
    {
        Assert.Equal("#31C99A", ToolDefaults.BorderColor);
        Assert.Equal(ToolDefaults.BorderColor, new DimRequest().BorderColor);
        Assert.Equal(ToolDefaults.BorderColor, new MagnifierRequest(MagnifierMode.Region).BorderColor);
        Assert.Equal(ToolDefaults.BorderColor, new SnapshotRequest().BorderColor);
        Assert.Equal(ToolDefaults.BorderColor, MagnifierFrameStyle.Default.BorderColor);
        Assert.Equal(ToolDefaults.BorderColor, SnapshotStyle.Default.BorderColor);
        foreach (var name in Plugins)
        {
            Assert.Equal(ToolDefaults.BorderColor, (string)Sample(name).Settings.Single(f => f.Key == "borderColor").Default!);
        }

        // Запрос без цвета (или с неверным) — цвет по умолчанию и на стороне хоста.
        Assert.Equal(ToolDefaults.BorderColor, PluginToolParser.Parse(new JsonObject { ["t"] = "tool.open", ["id"] = "f", ["kind"] = "dim" }, out _)!.Dim!.BorderColor);
        Assert.Equal(ToolDefaults.BorderColor, PluginToolParser.Parse(new JsonObject { ["t"] = "tool.open", ["id"] = "l", ["kind"] = "magnifier", ["mode"] = "region", ["borderColor"] = "gray" }, out _)!.Magnifier!.BorderColor);
        Assert.Equal(ToolDefaults.BorderColor, PluginToolParser.Parse(new JsonObject { ["t"] = "tool.open", ["id"] = "s", ["kind"] = "snapshot" }, out _)!.Snapshot!.BorderColor);
    }

    [Fact]
    public void Clean_installation_shows_the_new_default_in_all_three_plugins()
    {
        var store = new PluginStore(_dir);
        foreach (var name in Plugins)
        {
            Assert.Equal("#31C99A", Border(store.LoadSettings(Sample(name))));
        }
    }

    [Theory]
    [InlineData("Focus")]
    [InlineData("Lens")]
    public void Old_default_saved_by_an_older_version_becomes_the_new_default_but_custom_colors_and_other_settings_stay(string name)
    {
        var m = Sample(name);
        var store = new PluginStore(_dir);

        // Прежняя версия сохраняла все поля страницы — нетронутый цвет тоже (#D4D4D4), и без отметки.
        File.WriteAllText(store.SettingsPath(m.Id), new JsonObject { ["borderColor"] = "#d4d4d4", ["radius"] = 8, ["borderWidth"] = 3.5 }.ToJsonString());
        var loaded = store.LoadSettings(m);
        Assert.Equal("#31C99A", Border(loaded));
        Assert.Equal(8, (double)loaded["radius"]!);                         // остальные настройки не меняются
        Assert.Equal(3.5, (double)loaded["borderWidth"]!);

        // Цвет, выбранный пользователем, сохраняется.
        File.WriteAllText(store.SettingsPath(m.Id), new JsonObject { ["borderColor"] = "#FF0000", ["radius"] = 8 }.ToJsonString());
        Assert.Equal("#FF0000", Border(store.LoadSettings(m)));

        // Записано этой версией (отметка): даже прежний серый — сознательный выбор и не меняется.
        var values = store.LoadSettings(m);
        values["borderColor"] = "#D4D4D4";
        store.SaveSettings(m, values);
        Assert.Equal("#D4D4D4", Border(store.LoadSettings(m)));
        Assert.True((bool)JsonNode.Parse(File.ReadAllText(store.SettingsPath(m.Id)))![PluginStore.ExplicitMarker]!);
        Assert.False(store.LoadSettings(m).ContainsKey(PluginStore.ExplicitMarker)); // плагину отметка не передаётся
    }

    [Fact]
    public void Profile_storage_applies_the_same_rule()
    {
        JsonObject? profile = new() { ["borderColor"] = "#D4D4D4", ["opacity"] = 40 };
        var store = new PluginStore(_dir) { ProfileLoad = _ => profile, ProfileSave = (_, v) => profile = v };
        var m = Sample("Focus");
        var loaded = store.LoadSettings(m);
        Assert.Equal("#31C99A", Border(loaded));
        Assert.Equal(40, (double)loaded["opacity"]!);
        loaded["opacity"] = 55;
        store.SaveSettings(m, loaded);                                        // пользователь изменил другое поле
        Assert.Equal("#31C99A", (string)profile!["borderColor"]!);
        Assert.Equal(55, (double)store.LoadSettings(m)["opacity"]!);
    }

    [Fact]
    public void Migration_from_the_former_built_in_tools_does_not_carry_the_old_default_but_carries_custom_colors()
    {
        var m = Sample("Focus");
        JsonObject Legacy(string color) => new()
        {
            ["focus"] = new JsonObject { ["mode"] = "dim", ["borderColor"] = color, ["radius"] = 8 },
            ["tools"] = new JsonObject { ["focus"] = true },
        };

        var plain = PluginMigration.Plan(m, Legacy("#D4D4D4"), _ => null, null, _ => false);
        Assert.False(plain.Settings?.ContainsKey("borderColor") ?? false);
        Assert.Equal(8, (double)plain.Settings!["radius"]!);
        var custom = PluginMigration.Plan(m, Legacy("#FF8800"), _ => null, null, _ => false);
        Assert.Equal("#FF8800", (string)custom.Settings!["borderColor"]!);
    }

    [Fact]
    public void Previous_defaults_are_optional_in_the_manifest_and_bounded()
    {
        var snapshot = Sample("SnapshotOverlay").Settings.Single(f => f.Key == "borderColor");
        Assert.Null(snapshot.PreviousDefaults);                                // не выпускался с другим цветом
        var focus = Sample("Focus").Settings.Single(f => f.Key == "borderColor");
        Assert.Equal(["\"#D4D4D4\""], focus.PreviousDefaults!.Select(x => x.ToJsonString()));
        Assert.True(PluginStore.IsPreviousDefault(focus, JsonValue.Create("#D4D4D4")!));
        Assert.False(PluginStore.IsPreviousDefault(focus, JsonValue.Create("#31C99A")!));
    }
}
