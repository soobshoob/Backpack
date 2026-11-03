using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Backpack.Sdk;
using Backpack.Visual.Commands;
using Backpack.Visual.Hotkeys;
using Backpack.Visual.Plugins;
using Backpack.Visual.Presentation;
using Backpack.Visual.Windows;
using SnapshotOverlay;

namespace Backpack.Visual.Tests;

/// <summary>
/// «Снимок поверх окон» (SDK 1.3, overlay.snapshot): manifest и оформление как у «Фокуса», граница протокола, хранение места,
/// сочетания, снимок в окнах хоста (захват, отмена Esc, замена, развёртывание двойным щелчком и Space над снимком, точное
/// свёртывание, автоматическое свёртывание, скрытие, удаление) и движок инструментов (Esc, блокировка, выключение плагина).
/// Настоящий захват экрана и ввод в другой программе проверяются вручную на Windows (docs/plugins/snapshot-overlay.md).
/// </summary>
public class SnapshotOverlayTests
{
    private const string PluginId = "com.example.snapshot-overlay";

    private static readonly MonitorDesc Primary = new("p", "Primary", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), 1.0, true);
    private static readonly MonitorDesc HiDpi = new("h", "4K", new PixelRect(1920, 0, 3840, 2160), new PixelRect(1920, 0, 3840, 2100), 1.5, false);

    private static PluginManifest Sample(string name)
    {
        var (m, errors) = PluginManifest.Parse(File.ReadAllText(Repo.PathOf("lab", "samples", name, "manifest.json")));
        Assert.True(m is not null, string.Join("; ", errors));
        return m!;
    }

    private static BitmapSource Bitmap(int w, int h)
    {
        var b = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, new byte[w * h * 4], w * 4);
        b.Freeze();
        return b;
    }

    private sealed class FakeKeys : IHotkeyApi
    {
        public Dictionary<int, (uint Mods, uint Vk)> Registered { get; } = new();

        public bool Busy { get; set; }

        public bool Register(nint hwnd, int id, uint modifiers, uint vk, out int error)
        {
            error = Busy ? 1409 : 0;
            if (!Busy)
            {
                Registered[id] = (modifiers, vk);
            }

            return !Busy;
        }

        public bool Unregister(nint hwnd, int id) => Registered.Remove(id);
    }

    /// <summary>Подменённое окружение: указатель, кнопки мыши, ввод текста, активное окно, захват — без экрана и ввода.</summary>
    private sealed class Probe
    {
        public (int X, int Y) Cursor { get; set; } = (960, 540);

        public bool Buttons { get; set; }

        public bool Typing { get; set; }

        public nint Foreground { get; set; } = 42;

        public FakeKeys Keys { get; } = new();

        public List<PixelRect> Captured { get; } = new();

        public Func<PixelRect, BitmapSource>? Capture { get; set; }

        public SnapshotEnvironment Env(params MonitorDesc[] monitors) => new()
        {
            Monitors = () => monitors,
            Cursor = () => Cursor,
            ButtonsDown = () => Buttons,
            Typing = () => Typing,
            Foreground = () => Foreground,
            Capture = r =>
            {
                Captured.Add(r);
                return Capture?.Invoke(r) ?? Bitmap(r.Width, r.Height);
            },
            HoverKey = () => new SnapshotHoverKey(Keys, createWindow: false),
            Animate = () => false,
            CaptureDelay = TimeSpan.Zero,
        };
    }

    private static void Ui(Action action) => Sta.Run(() =>
    {
        InteractiveOverlayWindow.TestNoActivate = true;
        // Продолжения async (захват после кадра без окна выбора) — в этом же потоке UI, как в приложении.
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try
        {
            action();
        }
        finally
        {
            InteractiveOverlayWindow.TestNoActivate = false;
        }
    });

    private static void PumpUntil(Func<bool> done, int ms = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (!done() && sw.ElapsedMilliseconds < ms)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
            Thread.Sleep(1);
        }

        Assert.True(done(), "timeout");
    }

    // ── manifest, оформление, сочетания ─────────────────────────────────────

    [Fact]
    public void Manifest_is_valid_sdk_1_3_and_registers_as_a_plugin_from_the_creator()
    {
        var m = Sample("SnapshotOverlay");
        Assert.Equal(PluginId, m.Id);
        Assert.Equal("1.3", m.Sdk);
        Assert.True(SdkInfo.IsCompatible(m.Sdk));
        Assert.True(m.Has(Capabilities.OverlaySnapshot));
        Assert.False(m.Has(Capabilities.Screen));                        // изображения экрана плагин не получает
        Assert.Equal("SnapshotOverlay.SnapshotPlugin", m.Type);
        Assert.Equal(typeof(SnapshotPlugin).FullName, m.Type);
        Assert.Equal("image", m.Symbol);
        Assert.True(PluginSymbols.IsChoosable(m.Symbol));
        Assert.True(m.Settings.Count <= 32);
        Assert.All(m.Settings, f => Assert.NotNull(f.Group));
        Assert.All(m.Settings, f => Assert.Equal(LocalizedText.Languages.Count, f.Title.Values.Count));
        Assert.Equal(LocalizedText.Languages.Count, m.Name.Values.Count);
        Assert.Equal(["capture", "toggleVisible", "expand", "clear", "edit", "reset"], m.Commands.Select(c => c.Id));
        Assert.All(m.Commands, c => Assert.True(Icons.Lucide.Has(c.Icon!), c.Id));
    }

    [Fact]
    public void Plugin_defaults_match_the_manifest_and_stay_within_their_ranges()
    {
        var m = Sample("SnapshotOverlay");
        Assert.Equal(m.Settings.Select(f => f.Key).Order(), SnapshotPlugin.Defaults.Keys.Order());
        foreach (var f in m.Settings)
        {
            var d = SnapshotPlugin.Defaults[f.Key];
            if (f.Type == PluginSettingType.Number)
            {
                Assert.Equal((double)f.Default!, (double)d!, 6);
            }
            else
            {
                Assert.Equal(f.Default!.ToJsonString(), d!.ToJsonString());
            }

            Assert.Equal(f.Default!.ToJsonString(), PluginStore.Sanitize(f, f.Default)!.ToJsonString()); // в пределах схемы
        }
    }

    [Fact]
    public void Default_look_is_the_focus_look_and_the_lens_frame_shadow()
    {
        var snap = Sample("SnapshotOverlay");
        var focus = Sample("Focus");
        var lens = Sample("Lens");
        string D(PluginManifest m, string key) => m.Settings.Single(f => f.Key == key).Default!.ToJsonString();
        foreach (var key in new[] { "radius", "borderWidth", "borderColor", "glow" })
        {
            Assert.Equal(D(focus, key), D(snap, key));
        }

        Assert.Equal(D(lens, "shadow"), D(snap, "shadow"));
        var s = SnapshotStyle.Default;
        var frame = MagnifierFrameStyle.Default; // рамка «Фокуса»/«Линзы» в хосте
        Assert.Equal((frame.Radius, frame.BorderWidth, frame.BorderColor, frame.Glow, frame.Shadow), (s.Radius, s.BorderWidth, s.BorderColor, s.Glow, s.Shadow));
        Assert.Equal((16.0, 0.55, "#000000"), (s.ShadowBlur, s.ShadowOpacity, s.ShadowColor)); // как тень MagnifierFrame
        Assert.Equal(1.0, s.Opacity);
        Assert.Equal(1.0, s.BorderOpacity);
    }

    [Fact]
    public void Default_hotkeys_are_valid_and_do_not_collide_with_other_backpack_defaults()
    {
        var snap = Sample("SnapshotOverlay");
        var others = new[] { "Drawing", "LayoutIndicator", "CursorZoom", "Lens", "Focus", "PresentationTimer" }
            .SelectMany(n => Sample(n).Commands).Select(c => c.Hotkey).OfType<string>()
            .Append("Ctrl+Alt+Shift+O") // встроенная «Показать / скрыть отображение»
            .ToHashSet();
        var defaults = snap.Commands.Where(c => c.Hotkey is not null).ToList();
        Assert.Equal(["Ctrl+Alt+Shift+S", "Ctrl+Alt+Shift+E"], defaults.Select(c => c.Hotkey));
        foreach (var c in defaults)
        {
            Assert.True(Hotkey.TryParse(c.Hotkey, out var hk));
            Assert.True(HotkeyRules.IsAcceptable(hk), c.Id);
            Assert.False(HotkeyRules.IsReserved(hk), c.Id);
            Assert.DoesNotContain(c.Hotkey!, others);
        }

        // Действия над открытым снимком — только пока он есть (whileTool); захват — всегда.
        Assert.False(snap.Commands.Single(c => c.Id == "capture").WhileTool);
        Assert.True(snap.Commands.Single(c => c.Id == "expand").WhileTool);
    }

    [Fact]
    public void While_tool_hotkeys_register_only_while_a_snapshot_exists_and_are_released()
    {
        var api = new FakeKeys();
        using var mgr = new HotkeyManager(api, createWindow: false);
        Assert.True(Hotkey.TryParse("Ctrl+Alt+Shift+S", out var capture));
        Assert.True(Hotkey.TryParse("Ctrl+Alt+Shift+E", out var expand));
        string cap = $"plugin.{PluginId}.capture", exp = $"plugin.{PluginId}.expand";
        mgr.ApplyCommands([(cap, capture, CommandScope.Global, CommandTool.None, true), (exp, expand, CommandScope.Tool, CommandTool.Plugin, true)]);
        Assert.Equal(HotkeyStatus.Registered, mgr.StatusOf(cap));
        Assert.Equal(HotkeyStatus.WaitingForMode, mgr.StatusOf(exp));
        Assert.Single(api.Registered);

        mgr.SetPluginToolActive(PluginId, true);
        Assert.Equal(HotkeyStatus.Registered, mgr.StatusOf(exp));
        Assert.Equal(2, api.Registered.Count);
        mgr.SetPluginToolActive(PluginId, false);
        Assert.Equal(HotkeyStatus.WaitingForMode, mgr.StatusOf(exp));

        // Плагин выключен: команды недоступны — ни одно сочетание не остаётся зарегистрированным.
        mgr.ApplyCommands([(cap, capture, CommandScope.Global, CommandTool.None, false), (exp, expand, CommandScope.Tool, CommandTool.Plugin, false)]);
        Assert.Empty(api.Registered);
    }

    // ── граница протокола ───────────────────────────────────────────────────

    [Fact]
    public void Host_parser_clamps_snapshot_values_and_rejects_unknown_actions()
    {
        var msg = Protocol.ToJson("snapshot", new SnapshotRequest(Place: "nowhere", Corner: "middle", StartSize: 1e9, Aspect: "zoom", Background: "red",
            MinSize: -5, MaxSize: 7, Opacity: 0, Radius: 999, BorderWidth: -1, BorderColor: "#12", BorderOpacity: 3, ShadowBlur: 1000, ExpandMonitor: "tv", Keep: "forever"));
        var r = PluginToolParser.Parse(msg, out string? kind)!.Snapshot!;
        Assert.Equal("snapshot", kind);
        Assert.Equal(("last", "topRight", "keep", "snapshot", "session"), (r.Place, r.Corner, r.Aspect, r.ExpandMonitor, r.Keep));
        Assert.Equal((2400.0, 48.0, 1.0, 0.2), (r.StartSize, r.MinSize, r.MaxSize, r.Opacity));
        Assert.Equal((64.0, 0.0, 1.0, 60.0), (r.Radius, r.BorderWidth, r.BorderOpacity, r.ShadowBlur));
        Assert.Equal(("#111513", ToolDefaults.BorderColor), (r.Background, r.BorderColor));
        Assert.Equal(new SnapshotRequest(), PluginToolParser.Parse(Protocol.ToJson("snapshot", new SnapshotRequest()), out _)!.Snapshot);
        Assert.Null(PluginToolParser.ParseSnapshotAction("undo"));
        Assert.Null(PluginToolParser.ParseSnapshotAction(null));
        Assert.Equal(SnapshotActions.ToggleExpand, PluginToolParser.ParseSnapshotAction("toggleExpand"));
    }

    [Fact]
    public void Process_accepts_snapshot_messages_only_with_the_overlay_snapshot_capability()
    {
        Sta.Run(() =>
        {
            var p = new PluginProcess(Sample("SnapshotOverlay"), Dispatcher.CurrentDispatcher);
            PluginToolRequest? opened = null;
            var actions = new List<string>();
            p.ToolRequested += r => opened = r;
            p.ToolActionRequested += (_, a) => actions.Add(a);
            p.BeginForTest();
            p.Handle("""{"t":"ready","sdk":"1.3"}""");
            Assert.Equal(PluginRunState.Running, p.State);
            p.Handle(Protocol.Write(Protocol.ToJson("snapshot", new SnapshotRequest(Radius: 4))));
            Assert.Equal(4, opened!.Snapshot!.Radius);
            p.Handle("""{"t":"tool.action","id":"snapshot","action":"toggleExpand"}""");
            p.Handle("""{"t":"tool.action","id":"snapshot","action":"resetPlace"}""");
            Assert.Equal(["toggleExpand", "resetPlace"], actions);
            p.Handle("""{"t":"tool.action","id":"snapshot","action":"undo"}"""); // действие холста без overlay.draw
            p.Handle("""{"t":"tool.open","id":"x","kind":"dim"}""");               // затемнение без overlay.dim
            Assert.Equal(2, p.Violations);
            p.Handle("""{"t":"tool.close","id":"snapshot"}""");
            Assert.Equal(2, p.Violations);

            var focus = new PluginProcess(Sample("Focus"), Dispatcher.CurrentDispatcher);
            focus.BeginForTest();
            focus.Handle("""{"t":"ready","sdk":"1.1"}""");
            focus.Handle(Protocol.Write(Protocol.ToJson("snapshot", new SnapshotRequest())));
            focus.Handle("""{"t":"tool.action","id":"snapshot","action":"toggleExpand"}""");
            Assert.Equal(2, focus.Violations);                                     // «Фокус» снимков открывать не может
        });
    }

    // ── хранение места ──────────────────────────────────────────────────────

    [Fact]
    public void Store_keeps_only_the_window_place_and_forgets_it_on_reset_and_removal()
    {
        string root = Path.Combine(Path.GetTempPath(), "bp-snap-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            var store = new PluginStore(root);
            Assert.Null(store.LoadToolBounds(PluginId, "snapshot"));
            store.SaveToolBounds(PluginId, "snapshot", new PixelRect(-1200, 40, 420, 280));
            Assert.Equal(new PixelRect(-1200, 40, 420, 280), new PluginStore(root).LoadToolBounds(PluginId, "snapshot"));
            var file = Directory.GetFiles(root, "*.tool.*.json").Single();
            var json = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            Assert.Equal(["height", "left", "top", "width"], json.Select(x => x.Key).Order()); // только геометрия, без изображения

            store.SaveToolBounds(PluginId, "../evil", new PixelRect(0, 0, 100, 100));          // неверный ID — ничего
            Assert.Single(Directory.GetFiles(root, "*.json", SearchOption.AllDirectories), f => f.Contains(".tool.", StringComparison.Ordinal));
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(root)!, "evil.json")));
            store.SaveToolBounds(PluginId, "snapshot", null);
            Assert.Null(store.LoadToolBounds(PluginId, "snapshot"));

            store.SaveToolBounds(PluginId, "snapshot", new PixelRect(0, 0, 400, 300));
            Assert.True(store.Remove(PluginId));
            Assert.Empty(Directory.GetFiles(root, "*.tool.*.json"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // ── снимок в окнах хоста ────────────────────────────────────────────────

    [Fact]
    public void Present_shows_the_snapshot_in_the_corner_with_original_resolution_and_saves_the_place()
    {
        Ui(() =>
        {
            var probe = new Probe();
            var places = new List<PixelRect?>();
            var c = new SnapshotController(() => new SnapshotRequest(), probe.Env(Primary));
            c.PlaceChanged += places.Add;
            var image = Bitmap(1200, 800);
            c.Present(image, new PixelRect(100, 100, 1200, 800));
            Assert.Equal(SnapshotState.Normal, c.State);
            Assert.Same(image, c.Image);
            Assert.Same(image, c.Window!.Image);
            Assert.Equal((1200, 800), c.ImageSize);                          // разрешение снимка не уменьшается
            Assert.Equal(new PixelRect(1476, 24, 420, 280), c.NormalRect);
            Assert.Equal(c.NormalRect, PhysicalPlacement.TargetOf(c.Window));
            Assert.True(c.Window.IsVisible);
            Assert.False(NativeWindow.HasNoActivateClickThrough(c.Window.Hwnd)); // принимает мышь, но не активируется
            Assert.Equal([new PixelRect(1476, 24, 420, 280)], places);
            Assert.False(c.ControlsVisible);
            c.RevealControls();
            Assert.True(c.ControlsVisible);
            Assert.NotNull(c.GripOf(FocusGripKind.Move));
            Assert.NotNull(c.GripOf(FocusGripKind.Resize));
            c.Dispose();
            Assert.Null(c.Image);
            Assert.Null(c.Window);
        });
    }

    [Fact]
    public void Double_click_expands_and_collapses_to_the_exact_geometry_without_drift_or_saving()
    {
        Ui(() =>
        {
            var probe = new Probe();
            var c = new SnapshotController(() => new SnapshotRequest(), probe.Env(Primary));
            int saves = 0;
            c.Present(Bitmap(1200, 800), new PixelRect(100, 100, 1200, 800));
            c.PlaceChanged += _ => saves++;
            var normal = c.NormalRect;
            c.DoubleClick();
            Assert.Equal(SnapshotState.Expanded, c.State);
            Assert.Equal(new PixelRect(252, 48, 1416, 944), c.Rect);
            Assert.Equal(c.Rect, PhysicalPlacement.TargetOf(c.Window!));
            Assert.Equal(1.0, c.Window!.Opacity);
            Assert.Null(c.GripOf(FocusGripKind.Move));                             // развёрнутый вид не перемещается
            Assert.Equal(normal, c.NormalRect);
            c.DragBy(false, 300, 300);                                             // перетаскивание развёрнутого — ничего
            c.EndDrag();
            Assert.Equal(normal, c.NormalRect);
            c.DoubleClick();
            Assert.Equal(SnapshotState.Normal, c.State);
            Assert.Equal(normal, c.Rect);
            for (int i = 0; i < 40; i++)
            {
                c.DoubleClick();
            }

            Assert.Equal(SnapshotState.Normal, c.State);
            Assert.Equal(normal, c.NormalRect);
            Assert.Equal(normal, PhysicalPlacement.TargetOf(c.Window));
            Assert.Equal(0, saves);                                                // развёртывание не перезаписывает место
            c.Dispose();
        });
    }

    [Fact]
    public void Double_click_can_be_turned_off_and_the_expand_command_still_works()
    {
        Ui(() =>
        {
            var req = new SnapshotRequest(DoubleClick: false);
            var c = new SnapshotController(() => req, new Probe().Env(Primary));
            c.Present(Bitmap(800, 600), new PixelRect(0, 0, 800, 600));
            c.DoubleClick();
            Assert.Equal(SnapshotState.Normal, c.State);
            c.ToggleExpand();
            Assert.Equal(SnapshotState.Expanded, c.State);
            c.ToggleExpand();
            Assert.Equal(SnapshotState.Normal, c.State);
            c.Dispose();
        });
    }

    [Fact]
    public void Space_is_taken_only_while_the_pointer_is_over_the_snapshot_and_nobody_is_typing()
    {
        Ui(() =>
        {
            var probe = new Probe();
            var req = new SnapshotRequest();
            var c = new SnapshotController(() => req, probe.Env(Primary));
            c.Present(Bitmap(1200, 800), new PixelRect(100, 100, 1200, 800));
            var normal = c.NormalRect;

            probe.Cursor = (100, 900);                                              // над CAD, не над снимком
            c.Tick();
            Assert.Empty(probe.Keys.Registered);

            probe.Cursor = (normal.Left + 10, normal.Top + 10);
            c.Tick();
            Assert.True(c.HoverKey!.Armed);
            Assert.Equal((0x4000u, 0x20u), probe.Keys.Registered.Values.Single());  // Space, MOD_NOREPEAT, без модификаторов
            c.HoverKey.Raise();
            Assert.Equal(SnapshotState.Expanded, c.State);
            probe.Cursor = (960, 500);
            c.Tick();                                                               // указатель над развёрнутым снимком
            Assert.True(c.HoverKey!.Armed);
            c.HoverKey.Raise();
            Assert.Equal(SnapshotState.Normal, c.State);
            Assert.Equal(normal, c.Rect);
            probe.Cursor = (normal.Left + 10, normal.Top + 10);

            probe.Typing = true;                                                    // в активном окне идёт ввод текста
            c.Tick();
            Assert.Empty(probe.Keys.Registered);
            probe.Typing = false;
            probe.Buttons = true;                                                   // перетаскивание, выделение — не перехватывать
            c.Tick();
            Assert.Empty(probe.Keys.Registered);
            probe.Buttons = false;
            c.Tick();
            Assert.Single(probe.Keys.Registered);

            probe.Cursor = (100, 900);
            c.Tick();
            Assert.Empty(probe.Keys.Registered);                                    // указатель ушёл — Space снова у программ

            req = req with { SpaceHover = false };
            c.ApplySettings();
            probe.Cursor = (normal.Left + 10, normal.Top + 10);
            c.Tick();
            Assert.Empty(probe.Keys.Registered);

            req = req with { SpaceHover = true };
            probe.Keys.Busy = true;                                                 // Space занят другой программой
            c.Tick();
            Assert.True(c.HoverKey!.Unavailable);
            c.Dispose();
            Assert.Empty(probe.Keys.Registered);
        });
    }

    [Fact]
    public void Expanded_snapshot_collapses_on_escape_a_new_click_outside_or_a_window_switch()
    {
        Ui(() =>
        {
            var probe = new Probe();
            var req = new SnapshotRequest();
            var c = new SnapshotController(() => req, probe.Env(Primary));
            c.Present(Bitmap(1200, 800), new PixelRect(100, 100, 1200, 800));
            var normal = c.NormalRect;

            Assert.False(c.Escape());                                               // в обычном виде Esc снимок не трогает
            Assert.Equal(SnapshotState.Normal, c.State);
            c.Expand();
            Assert.True(c.Escape());
            Assert.Equal(normal, c.Rect);

            // Нажатие, которое было при развёртывании, не считается щелчком вне снимка.
            probe.Buttons = true;
            probe.Cursor = (5, 5);
            c.Expand();
            c.Tick();
            Assert.Equal(SnapshotState.Expanded, c.State);
            probe.Buttons = false;
            c.Tick();
            Assert.Equal(SnapshotState.Expanded, c.State);
            probe.Cursor = (960, 500);                                              // щелчок по самому снимку — не свёртывает
            probe.Buttons = true;
            c.Tick();
            Assert.Equal(SnapshotState.Expanded, c.State);
            probe.Buttons = false;
            c.Tick();
            probe.Cursor = (5, 5);
            probe.Buttons = true;                                                   // новый щелчок вне снимка
            c.Tick();
            Assert.Equal(SnapshotState.Normal, c.State);
            Assert.Equal(normal, c.Rect);
            probe.Buttons = false;
            c.Tick();

            c.Expand();
            probe.Foreground = 77;                                                  // Alt+Tab в другую программу
            c.Tick();
            Assert.Equal(SnapshotState.Normal, c.State);

            req = req with { AutoCollapse = false };
            c.Expand();
            probe.Foreground = 78;
            probe.Buttons = true;
            c.Tick();
            Assert.Equal(SnapshotState.Expanded, c.State);
            Assert.True(c.Escape());
            Assert.Equal(normal, c.NormalRect);
            c.Dispose();
        });
    }

    [Fact]
    public void Capture_hides_backpack_windows_first_and_keeps_the_full_resolution()
    {
        Ui(() =>
        {
            var probe = new Probe();
            SnapshotController? c = null;
            bool selectionOpen = true, snapshotVisible = true;
            probe.Capture = r =>
            {
                selectionOpen = c!.Selection is not null;
                snapshotVisible = c.Window?.IsVisible == true;
                return Bitmap(r.Width, r.Height);
            };
            c = new SnapshotController(() => new SnapshotRequest(Place: "capture"), probe.Env(Primary));
            c.StartCapture();
            Assert.Equal(SnapshotState.Selecting, c.State);
            Assert.NotNull(c.Selection);
            var task = c.CaptureAreaAsync(new PixelRect(10, 20, 300, 200));
            PumpUntil(() => task.IsCompleted);
            Assert.True(task.Result);
            Assert.False(selectionOpen);                                            // окно выбора в снимок не попадает
            Assert.Equal(new PixelRect(10, 20, 300, 200), probe.Captured.Single());
            Assert.Equal((300, 200), c.ImageSize);
            Assert.Equal(new PixelRect(10, 20, 300, 200), c.NormalRect);           // «поверх снятой области» 1:1

            // Замена: прежний снимок скрыт во время выбора и захвата.
            var first = c.Image;
            c.StartCapture();
            Assert.False(c.Window!.IsVisible);
            task = c.CaptureAreaAsync(new PixelRect(500, 500, 400, 100));
            PumpUntil(() => task.IsCompleted);
            Assert.False(snapshotVisible);
            Assert.NotSame(first, c.Image);
            Assert.Equal((400, 100), c.ImageSize);
            Assert.True(c.Window!.IsVisible);
            c.Dispose();
        });
    }

    [Fact]
    public void Cancelling_a_replacement_keeps_the_old_snapshot_and_cancelling_the_first_closes()
    {
        Ui(() =>
        {
            var probe = new Probe();
            var reasons = new List<string>();
            var c = new SnapshotController(() => new SnapshotRequest(), probe.Env(Primary));
            c.Closed += reasons.Add;
            c.Present(Bitmap(800, 600), new PixelRect(0, 0, 800, 600));
            var image = c.Image;
            var rect = c.NormalRect;
            c.StartCapture();
            Assert.True(c.WantsEscape);
            Assert.True(c.Escape());                                                // Esc — отмена выбора
            Assert.Equal(SnapshotState.Normal, c.State);
            Assert.Same(image, c.Image);
            Assert.Equal(rect, c.NormalRect);
            Assert.True(c.Window!.IsVisible);
            Assert.Empty(reasons);

            c.Hide();
            c.StartCapture();
            c.CancelCapture();
            Assert.Equal(SnapshotState.Hidden, c.State);                            // был скрыт — остаётся скрытым
            c.Dispose();

            var fresh = new SnapshotController(() => new SnapshotRequest(), probe.Env(Primary));
            fresh.Closed += reasons.Add;
            fresh.StartCapture();
            fresh.Escape();
            Assert.Equal(["cancelled"], reasons);
            Assert.Equal(SnapshotState.None, fresh.State);
            Assert.Null(fresh.Selection);
        });
    }

    [Fact]
    public void Failed_capture_keeps_the_previous_snapshot_or_reports_unavailable()
    {
        Ui(() =>
        {
            var probe = new Probe { Capture = _ => throw new InvalidOperationException("BitBlt failed") };
            var reasons = new List<string>();
            var c = new SnapshotController(() => new SnapshotRequest(), probe.Env(Primary));
            c.Closed += reasons.Add;
            c.Present(Bitmap(800, 600), new PixelRect(0, 0, 800, 600));
            var image = c.Image;
            c.StartCapture();
            var t = c.CaptureAreaAsync(new PixelRect(0, 0, 50, 50));
            PumpUntil(() => t.IsCompleted);
            Assert.False(t.Result);
            Assert.Same(image, c.Image);
            Assert.Equal(SnapshotState.Normal, c.State);
            c.Dispose();

            var fresh = new SnapshotController(() => new SnapshotRequest(), probe.Env(Primary));
            fresh.Closed += reasons.Add;
            fresh.StartCapture();
            t = fresh.CaptureAreaAsync(new PixelRect(0, 0, 50, 50));
            PumpUntil(() => t.IsCompleted);
            Assert.Equal(["unavailable"], reasons);
        });
    }

    [Fact]
    public void Replacement_appears_where_the_user_left_the_previous_snapshot()
    {
        Ui(() =>
        {
            var probe = new Probe();
            var c = new SnapshotController(() => new SnapshotRequest(), probe.Env(Primary));
            c.Present(Bitmap(1200, 800), new PixelRect(100, 100, 1200, 800));
            c.DragBy(false, -1000, 500);
            c.EndDrag();
            var moved = c.NormalRect;
            Assert.Equal(new PixelRect(476, 524, 420, 280), moved);
            c.Present(Bitmap(1000, 1000), new PixelRect(0, 0, 1000, 1000));
            Assert.Equal(new PixelRect(476, 524, 420, 420) with { Top = Math.Min(524, 1040 - 420) }, c.NormalRect);
            Assert.Equal(c.NormalRect, c.SavedPlace);
            c.Dispose();

            // Новый сеанс: место из хранилища.
            var again = new SnapshotController(() => new SnapshotRequest(), probe.Env(Primary), saved: moved);
            again.Present(Bitmap(1200, 800), new PixelRect(0, 0, 1200, 800));
            Assert.Equal(moved, again.NormalRect);
            again.ResetPlace();
            Assert.Null(again.SavedPlace);
            Assert.Equal(new PixelRect(1476, 24, 420, 280), again.NormalRect);
            again.Dispose();
        });
    }

    [Fact]
    public void Grips_resize_with_the_aspect_ratio_and_moving_to_another_dpi_keeps_the_dip_size()
    {
        Ui(() =>
        {
            var probe = new Probe();
            var req = new SnapshotRequest();
            var c = new SnapshotController(() => req, probe.Env(Primary, HiDpi));
            var places = new List<PixelRect?>();
            c.PlaceChanged += places.Add;
            c.Present(Bitmap(1200, 800), new PixelRect(100, 100, 1200, 800));
            c.DragBy(true, 120, 0);
            c.EndDrag();
            Assert.Equal(SnapshotGeometry.HeightFor(c.NormalRect.Width, 1200, 800), c.NormalRect.Height);
            Assert.True(c.NormalRect.Width > 420);
            Assert.Equal(c.NormalRect, places[^1]);

            // На монитор 150 %: тот же размер в DIP, целиком в рабочей области.
            var before = c.NormalRect;
            c.DragBy(false, 1500, 300);
            c.EndDrag();
            Assert.Equal((int)Math.Round(before.Width * 1.5), c.NormalRect.Width);
            Assert.True(c.NormalRect.Left >= HiDpi.Work.Left && c.NormalRect.Right <= HiDpi.Work.Right);
            c.Expand();
            Assert.True(c.Rect.Left >= HiDpi.Work.Left);                            // развёрнут на мониторе снимка
            Assert.InRange(c.Rect.Height / (double)HiDpi.Work.Height, 0.85, 0.95);
            c.Collapse();

            // Свободные пропорции.
            req = req with { Aspect = "stretch" };
            c.ApplySettings();
            var free = c.NormalRect;
            c.DragBy(true, 200, -50);
            c.EndDrag();
            Assert.Equal((free.Width + 200, free.Height - 50), (c.NormalRect.Width, c.NormalRect.Height));
            req = req with { Aspect = "keep" };
            c.ApplySettings();
            Assert.Equal(SnapshotGeometry.HeightFor(c.NormalRect.Width, 1200, 800), c.NormalRect.Height);
            c.Dispose();
        });
    }

    [Fact]
    public void Hide_show_and_clear_follow_the_snapshot_lifecycle()
    {
        Ui(() =>
        {
            var probe = new Probe();
            var states = new List<SnapshotState>();
            var reasons = new List<string>();
            var c = new SnapshotController(() => new SnapshotRequest(), probe.Env(Primary));
            c.StateChanged += states.Add;
            c.Closed += reasons.Add;
            c.Present(Bitmap(640, 480), new PixelRect(0, 0, 640, 480));
            var rect = c.NormalRect;
            c.Expand();
            c.ToggleVisible();
            Assert.Equal(SnapshotState.Hidden, c.State);
            Assert.False(c.Window!.IsVisible);
            Assert.NotNull(c.Image);                                                // скрыт — изображение в памяти
            c.Expand();                                                             // скрытый не разворачивается
            Assert.Equal(SnapshotState.Hidden, c.State);
            c.ToggleVisible();
            Assert.Equal(SnapshotState.Normal, c.State);
            Assert.Equal(rect, c.Rect);
            Assert.True(c.Window.IsVisible);
            c.Clear();
            Assert.Equal(SnapshotState.None, c.State);
            Assert.Null(c.Image);
            Assert.Null(c.Window);
            Assert.Equal(["cleared"], reasons);
            Assert.Equal([SnapshotState.Normal, SnapshotState.Expanded, SnapshotState.Normal, SnapshotState.Hidden, SnapshotState.Normal, SnapshotState.None], states);
        });
    }

    [Fact]
    public void Style_options_click_through_and_layer_apply_to_the_open_snapshot()
    {
        Ui(() =>
        {
            var req = new SnapshotRequest();
            var c = new SnapshotController(() => req, new Probe().Env(Primary));
            c.Present(Bitmap(640, 480), new PixelRect(0, 0, 640, 480));
            Assert.True(c.Frame!.IsVisible);                                        // тень по умолчанию
            req = req with { Shadow = false, Opacity = 0.6, ClickThrough = true, Topmost = false };
            c.ApplySettings();
            Assert.False(c.Frame.IsVisible);
            Assert.Equal(0.6, c.Window!.Opacity, 3);
            Assert.True(c.Window.ClickThrough);
            Assert.True(NativeWindow.HasNoActivateClickThrough(c.Window.Hwnd));
            c.Expand();
            Assert.Equal(1.0, c.Window.Opacity);                                    // развёрнутый — непрозрачный
            c.Dispose();
        });
    }

    // ── движок инструментов ─────────────────────────────────────────────────

    [Fact]
    public void Engine_opens_one_snapshot_per_id_routes_actions_and_escape_and_cleans_up()
    {
        Ui(() =>
        {
            var probe = new Probe();
            var saved = new Dictionary<string, PixelRect?>();
            var closed = new List<(string Plugin, string Tool, string Reason)>();
            using var e = new PluginToolEngine(() => [Primary])
            {
                SnapshotEnvironment = probe.Env(Primary),
                LoadPlace = (_, _) => null,
                SavePlace = (p, t, r) => saved[p + "/" + t] = r,
            };
            e.Closed += (p, t, r) => closed.Add((p, t, r));

            // Новый снимок начинает выбор; Esc отменяет — снимка нет, сессия закрыта.
            e.Open(PluginId, new PluginToolRequest("snapshot", null, null, null, new SnapshotRequest()));
            Assert.Equal(SnapshotState.Selecting, e.SnapshotOf(PluginId, "snapshot")!.State);
            Assert.Contains(PluginId, e.PluginsWithOpenTools);                       // сочетания whileTool действуют
            Assert.True(e.Escape());
            Assert.Equal(0, e.Count);
            Assert.Equal([(PluginId, "snapshot", "cancelled")], closed);

            e.Open(PluginId, new PluginToolRequest("snapshot", null, null, null, new SnapshotRequest()));
            var snap = e.SnapshotOf(PluginId, "snapshot")!;
            snap.Present(Bitmap(1200, 800), new PixelRect(0, 0, 1200, 800));
            Assert.NotNull(saved[PluginId + "/snapshot"]);
            Assert.False(e.Escape());                                               // Esc в CAD не удаляет снимок
            Assert.Equal(1, e.Count);

            // Повтор open с тем же ID — только параметры, без нового выбора.
            e.Open(PluginId, new PluginToolRequest("snapshot", null, null, null, new SnapshotRequest(Radius: 4)));
            Assert.Equal(SnapshotState.Normal, snap.State);
            Assert.Same(snap, e.SnapshotOf(PluginId, "snapshot"));

            e.Action(PluginId, "snapshot", SnapshotActions.ToggleExpand);
            Assert.Equal(SnapshotState.Expanded, snap.State);
            Assert.True(e.Escape());
            Assert.Equal(SnapshotState.Normal, snap.State);
            e.Action(PluginId, "snapshot", SnapshotActions.Hide);
            Assert.Equal(SnapshotState.Hidden, snap.State);
            e.Action(PluginId, "snapshot", SnapshotActions.Show);
            Assert.Equal(SnapshotState.Normal, snap.State);
            e.Action(PluginId, "snapshot", SnapshotActions.Capture);
            Assert.Equal(SnapshotState.Selecting, snap.State);
            e.Action(PluginId, "snapshot", SnapshotActions.Collapse);               // во время выбора — ничего
            Assert.True(e.Escape());
            Assert.Equal(SnapshotState.Normal, snap.State);
            e.Edit(PluginId, "snapshot");
            Assert.True(snap.ControlsVisible);

            // Блокировка: снимок остаётся (по умолчанию), развёрнутый сворачивается.
            snap.Expand();
            e.CloseAllSystem("system");
            Assert.Equal(1, e.Count);
            Assert.Equal(SnapshotState.Normal, snap.State);
            // Скрытие отображения Backpack: снимок скрывается, изображение остаётся.
            e.CloseAllSystem("host", keepSnapshots: true);
            Assert.Equal(SnapshotState.Hidden, snap.State);
            Assert.NotNull(snap.Image);

            // Выключение плагина: всё закрыто, изображение освобождено, плагину событие не нужно.
            closed.Clear();
            e.CloseAll(PluginId);
            Assert.Equal(0, e.Count);
            Assert.Null(snap.Image);
            Assert.Empty(closed);

            // Сброс места без открытого снимка.
            e.Action(PluginId, "snapshot", SnapshotActions.ResetPlace);
            Assert.Null(saved[PluginId + "/snapshot"]);
        });
    }

    [Fact]
    public void Engine_clears_snapshots_on_lock_when_asked_and_on_exit()
    {
        Ui(() =>
        {
            var probe = new Probe();
            using var e = new PluginToolEngine(() => [Primary]) { SnapshotEnvironment = probe.Env(Primary) };
            var closed = new List<string>();
            e.Closed += (_, _, r) => closed.Add(r);
            e.Open(PluginId, new PluginToolRequest("snapshot", null, null, null, new SnapshotRequest(Keep: "unlocked")));
            e.SnapshotOf(PluginId, "snapshot")!.Present(Bitmap(100, 100), new PixelRect(0, 0, 100, 100));
            e.CloseAllSystem("system");
            Assert.Equal(0, e.Count);
            Assert.Equal(["system"], closed);

            e.Open(PluginId, new PluginToolRequest("snapshot", null, null, null, new SnapshotRequest()));
            var snap = e.SnapshotOf(PluginId, "snapshot")!;
            snap.Present(Bitmap(100, 100), new PixelRect(0, 0, 100, 100));
            e.CloseAllSystem("host");                                               // выход из Backpack
            Assert.Equal(0, e.Count);
            Assert.Null(snap.Image);
            Assert.Equal(["system", "host"], closed);
        });
    }

    [Fact]
    public void Escape_order_with_focus_is_unchanged_and_a_just_expanded_snapshot_goes_first()
    {
        Ui(() =>
        {
            var probe = new Probe();
            using var e = new PluginToolEngine(() => [Primary]) { SnapshotEnvironment = probe.Env(Primary), Animate = () => false };
            e.Open(PluginId, new PluginToolRequest("snapshot", null, null, null, new SnapshotRequest()));
            var snap = e.SnapshotOf(PluginId, "snapshot")!;
            snap.Present(Bitmap(400, 300), new PixelRect(0, 0, 400, 300));
            e.Open("com.example.focus", new PluginToolRequest("focus", null, new DimRequest()));
            Assert.Equal(2, e.Count);

            snap.Expand();                                                          // последнее действие — со снимком
            Assert.True(e.Escape());
            Assert.Equal(SnapshotState.Normal, snap.State);
            Assert.Equal(2, e.Count);
            Assert.True(e.Escape());                                                // теперь — «Фокус», как раньше
            Assert.Single(e.SessionsOf(PluginId).ToList().Concat(e.SessionsOf("com.example.focus")));
            Assert.False(e.IsOpen("com.example.focus", "focus"));
            Assert.False(e.Escape());                                               // снимок в обычном виде Esc не трогает
            Assert.True(e.IsOpen(PluginId, "snapshot"));
        });
    }
}
