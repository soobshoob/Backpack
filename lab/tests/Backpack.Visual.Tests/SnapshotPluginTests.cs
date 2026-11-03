using System.IO;
using System.Text.Json.Nodes;
using Backpack.Sdk;
using SnapshotOverlay;

namespace Backpack.Visual.Tests;

/// <summary>
/// Плагин «Снимок поверх окон» через настоящий цикл сообщений SDK (<see cref="PluginRunner"/>), без отдельного процесса: команды
/// → сообщения хосту, закрытие снимка хостом, изменение настроек, сброс. Плагин не получает ни изображения, ни координат.
/// </summary>
public class SnapshotPluginTests
{
    private static List<JsonObject> Run(params JsonObject[] input)
    {
        var lines = string.Join("\n", input.Select(m => m.ToJsonString())) + "\n";
        var output = new StringWriter();
        int code = PluginRunner.Run(new SnapshotPlugin(), new StringReader(lines), output);
        Assert.Equal(0, code);
        return output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonNode.Parse(l)!.AsObject()).ToList();
    }

    private static JsonObject Hello(JsonObject? settings = null) => new() { ["t"] = Protocol.Hello, ["sdk"] = SdkInfo.Version, ["locale"] = "en", ["settings"] = settings ?? new JsonObject() };

    private static JsonObject Command(string id) => new() { ["t"] = Protocol.Command, ["id"] = id };

    private static JsonObject Closed(string reason) => new() { ["t"] = Protocol.ToolClosed, ["id"] = SnapshotPlugin.Tool, ["reason"] = reason };

    private static JsonObject Defaults()
    {
        var o = new JsonObject();
        foreach (var (k, v) in SnapshotPlugin.Defaults)
        {
            o[k] = v?.DeepClone();
        }

        return o;
    }

    [Fact]
    public void Sdk_1_3_is_compatible_and_older_plugins_still_load()
    {
        Assert.Equal("1.3", SdkInfo.Version);
        Assert.True(SdkInfo.IsCompatible("1.3"));
        Assert.True(SdkInfo.IsCompatible("1.1"));
        Assert.False(SdkInfo.IsCompatible("1.4"));
        Assert.Contains(Capabilities.OverlaySnapshot, Capabilities.Known);
        Assert.DoesNotContain(DrawActions.Undo, SnapshotActions.All); // имена действий холста и снимка не пересекаются
    }

    [Fact]
    public void Number_setting_reads_values_a_plugin_wrote_as_integers()
    {
        // Регрессия: после UpdateSettings целое значение в памяти плагина читалось как double с исключением.
        var s = new Dictionary<string, JsonNode?> { ["a"] = 16, ["b"] = 2.5, ["c"] = "x" };
        Assert.Equal(16, s.Number("a", 0));
        Assert.Equal(2.5, s.Number("b", 0));
        Assert.Equal(7, s.Number("c", 7));
        Assert.Equal(7, s.Number("missing", 7));
    }

    [Fact]
    public void Default_settings_produce_the_sdk_default_request()
    {
        var o = Run(Hello(Defaults()), Command("capture"), new JsonObject { ["t"] = Protocol.Stop });
        Assert.Equal(Protocol.Ready, (string?)o[0]["t"]);
        Assert.Equal("1.3", (string?)o[0]["sdk"]);
        var open = o[1];
        Assert.Equal(Protocol.Write(Protocol.ToJson(SnapshotPlugin.Tool, new SnapshotRequest())), Protocol.Write(open));
    }

    [Fact]
    public void Capture_opens_then_replaces_and_commands_map_to_host_actions()
    {
        var o = Run(
            Hello(),
            Command("expand"),          // снимка ещё нет — ничего
            Command("capture"),         // новый снимок: выбор области
            Command("capture"),         // снимок открыт: заменить
            Command("expand"),
            Command("toggleVisible"),
            Command("edit"),
            Closed("cleared"),          // пользователь удалил снимок кнопкой
            Command("expand"),          // ничего
            Command("capture"),         // снова новый снимок
            Command("clear"),
            Command("clear"),           // уже закрыт — ничего
            new JsonObject { ["t"] = Protocol.Stop });
        var sent = o.Skip(1).Select(m => ((string?)m["t"], (string?)m["kind"] ?? (string?)m["action"])).ToList();
        Assert.Equal(
        [
            (Protocol.ToolOpen, "snapshot"),
            (Protocol.ToolAction, SnapshotActions.Capture),
            (Protocol.ToolAction, SnapshotActions.ToggleExpand),
            (Protocol.ToolAction, SnapshotActions.ToggleVisible),
            (Protocol.ToolEdit, null),
            (Protocol.ToolOpen, "snapshot"),
            (Protocol.ToolClose, null),
        ], sent);
        Assert.All(o.Skip(1), m => Assert.Equal(SnapshotPlugin.Tool, (string?)m["id"]));
    }

    [Fact]
    public void Settings_change_updates_the_open_snapshot_in_place_and_percentages_become_shares()
    {
        var o = Run(
            Hello(),
            new JsonObject { ["t"] = Protocol.SettingsChanged, ["values"] = new JsonObject { ["radius"] = 4 } }, // снимка нет — ничего
            Command("capture"),
            new JsonObject
            {
                ["t"] = Protocol.SettingsChanged,
                ["values"] = new JsonObject { ["radius"] = 6, ["opacity"] = 80, ["shadowOpacity"] = 30, ["maxSize"] = 75, ["borderOpacity"] = 50, ["aspect"] = "fit", ["spaceHover"] = false },
            },
            new JsonObject { ["t"] = Protocol.Stop });
        Assert.Equal(3, o.Count);
        var update = o[2];
        Assert.Equal(Protocol.ToolOpen, (string?)update["t"]);
        Assert.Equal(6, (double)update["radius"]!);
        Assert.Equal(0.8, (double)update["opacity"]!, 6);
        Assert.Equal(0.3, (double)update["shadowOpacity"]!, 6);
        Assert.Equal(0.75, (double)update["maxSize"]!, 6);
        Assert.Equal(0.5, (double)update["borderOpacity"]!, 6);
        Assert.Equal("fit", (string?)update["aspect"]);
        Assert.False((bool)update["spaceHover"]!);
    }

    [Fact]
    public void Reset_writes_default_settings_and_forgets_the_saved_place()
    {
        var o = Run(Hello(new JsonObject { ["radius"] = 3 }), Command("reset"), Command("capture"), Command("reset"), new JsonObject { ["t"] = Protocol.Stop });
        Assert.Equal(Protocol.SettingsUpdate, (string?)o[1]["t"]);
        var values = o[1]["values"]!.AsObject();
        Assert.Equal(SnapshotPlugin.Defaults.Keys.Order(), values.Select(x => x.Key).Order());
        Assert.Equal(16, (int)values["radius"]!);
        Assert.Equal((Protocol.ToolAction, SnapshotActions.ResetPlace), ((string?)o[2]["t"], (string?)o[2]["action"]));
        Assert.Equal(16, (double)o[3]["radius"]!);                       // новый снимок — уже с настройками по умолчанию
        Assert.Equal(Protocol.SettingsUpdate, (string?)o[4]["t"]);
        Assert.Equal(Protocol.ToolOpen, (string?)o[5]["t"]);              // открытый снимок получает параметры по умолчанию
        Assert.Equal(SnapshotActions.ResetPlace, (string?)o[6]["action"]);
    }

    [Fact]
    public void Plugin_messages_never_carry_images_or_coordinates()
    {
        var o = Run(Hello(Defaults()), Command("capture"), Command("expand"), Command("reset"), new JsonObject { ["t"] = Protocol.Stop });
        foreach (var m in o)
        {
            foreach (var key in new[] { "image", "pixels", "x", "y", "left", "top", "bounds", "rect" })
            {
                Assert.False(m.ContainsKey(key), key);
            }
        }
    }
}
