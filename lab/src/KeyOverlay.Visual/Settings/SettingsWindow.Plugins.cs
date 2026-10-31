using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using KeyOverlay.Visual.Localization;
using KeyOverlay.Visual.Plugins;

namespace KeyOverlay.Visual.Settings;

/// <summary>
/// Phase 4G: страница плагина (настройки по группам манифеста, условные поля, своё сочетание у каждой команды, демонстрация
/// по манифесту) и перенос прежних встроенных инструментов в поставляемые плагины.
/// </summary>
public sealed partial class SettingsWindow
{
    /// <summary>Текущие значения открытого плагина (для демонстрации; обновляются при изменении профиля).</summary>
    private JsonObject? _pluginValues;

    private (InstalledPlugin Plugin, PluginPreview Preview)? PluginPreviewOf() =>
        Section == SettingsSection.Plugins && _pluginPage is { } id && _actions.Plugins?.Store.Get(id) is { Manifest.Preview: { } p } pl ? (pl, p) : null;

    /// <summary>
    /// Сцена демонстрации плагина. Анимированные сцены (увеличение, затемнение, панель) — <see cref="PluginPreviews"/>;
    /// SDK 1.2 добавляет статические сцены хоста: <c>drawing</c> — виртуальный монитор с настоящей панелью рисования в месте и
    /// режиме из настроек (как прежнее превью страницы «Рисование») и <c>indicator</c> — индикатор с выбранным оформлением.
    /// </summary>
    private FrameworkElement? PluginScene(PluginPreview p)
    {
        var values = _pluginValues ?? new JsonObject();
        string? Key(string param) => p.Bind.TryGetValue(param, out var k) ? k : null;
        switch (p.Scene)
        {
            case "drawing":
                _livePreview = null;
                var r = PluginToolParser.Parse(BindDraw(p, values), out _)?.Draw ?? new Backpack.Sdk.DrawRequest();
                return DrawingPreview(PluginDrawHost.ToSettings(r), PluginDrawHost.ToProfile(r));
            case "indicator":
                _livePreview = null;
                bool custom = Key("style") is { } sk && values[sk] is JsonValue sv && sv.TryGetValue(out string? style) && style == "custom";
                var visual = new Layout.LayoutIndicatorVisual { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                visual.Set(new Layout.LayoutLabel("RU", Loc.T("Layout.ExampleDetail")), Layout.IndicatorLook.Resolve(_store.Current.Appearance, LayoutIndicatorSettings.Default with { CustomStyle = custom }));
                return Stage.Wrap(new Viewbox { StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(16), Child = visual });
            default:
                _livePreview = PluginPreviews.Create(p, () => _pluginValues ?? new JsonObject(), AnimateDemos);
                return _livePreview is null ? null : Stage.Wrap(_livePreview);
        }
    }

    /// <summary>Значения настроек → сообщение tool.open вида draw по привязке сцены (то же ограничение значений, что у запроса плагина).</summary>
    private static JsonObject BindDraw(PluginPreview p, JsonObject values)
    {
        var msg = new JsonObject { ["t"] = "tool.open", ["id"] = "preview", ["kind"] = "draw" };
        foreach (var (param, key) in p.Bind)
        {
            if (values[key] is { } v)
            {
                msg[param] = param is "toolbarX" or "toolbarY" && v is JsonValue n && n.TryGetValue(out double d) && d < 0 ? null : v.DeepClone();
            }
        }

        return msg;
    }

    /// <summary>Значения плагина изменились: анимированная сцена обновляется на месте, статическая строится заново.</summary>
    private void RefreshPluginScene()
    {
        if (_livePreview is not null)
        {
            _livePreview.Refresh();
        }
        else if (PluginPreviewOf() is { } scene)
        {
            _previewHost.Content = PluginScene(scene.Preview);
        }
    }

    private void RefreshPluginValues()
    {
        _pluginValues = _pluginPage is { } id && _actions.Plugins is { } pm && pm.Store.Get(id) is { } pl ? pm.Store.LoadSettings(pl.Manifest) : null;
    }

    private static bool Visible(PluginSetting f, JsonObject values) =>
        f.ShowWhen is not { } w || (values[w.Key] is JsonValue v && v.ToJsonString().Trim('"') is var s && w.Values.Contains(s));

    /// <summary>
    /// Настройки плагина: карточка на группу манифеста (порядок — как в манифесте). Поле с условием <c>showWhen</c> показывается
    /// только при подходящем значении; скрытое значение не меняется и не теряется. Изменение поля-условия перестраивает страницу.
    /// </summary>
    private void AddPluginSettings(PluginManager pm, PluginManifest m)
    {
        var values = pm.Store.LoadSettings(m);
        _pluginValues = values;
        var controllers = m.Settings.Where(f => f.ShowWhen is not null).Select(f => f.ShowWhen!.Value.Key).ToHashSet(StringComparer.Ordinal);
        var groups = new List<(string Title, List<UIElement> Rows)>();
        foreach (var f in m.Settings)
        {
            if (f.Hidden || !Visible(f, values))
            {
                continue;
            }

            string key = f.Key;
            void Set(JsonNode? v)
            {
                var next = pm.Store.LoadSettings(m);
                next[key] = v;
                pm.SettingsChanged(m.Id, next);
                _pluginValues = pm.Store.LoadSettings(m);
                RefreshPluginScene();
                if (controllers.Contains(key))
                {
                    Dispatcher.BeginInvoke(BuildSection);
                }
            }

            double Num() => values[key] is JsonValue n && n.TryGetValue(out double d) ? d : f.Min;
            string Str() => values[key] is JsonValue t && t.TryGetValue(out string? s) && s is not null ? s : "";
            FrameworkElement control = f.Type switch
            {
                PluginSettingType.Toggle => _ui.Toggle(values[key] is JsonValue b && b.TryGetValue(out bool on) && on, v => Set(JsonValue.Create(v))),
                PluginSettingType.Number => _ui.Number(Num(), (f.Min, f.Max), f.Step, f.Unit ?? "", v => Set(JsonValue.Create(v))),
                PluginSettingType.Choice => _ui.Segmented(Math.Max(0, f.Choices.ToList().IndexOf(Str())),
                    f.Choices.Select((c, i) => f.ChoiceTitles is { } t && i < t.Count ? t[i].For(Loc.Code) : c).ToArray(), i => Set(JsonValue.Create(f.Choices[i]))),
                PluginSettingType.Color => _ui.Color(ThemeColor.TryParse(Str(), out var col) ? col : new ThemeColor(0x31, 0xC9, 0x9A), false, c => Set(JsonValue.Create(c.ToString()))),
                _ => PluginText(Str(), v => Set(JsonValue.Create(v))),
            };
            string group = f.Group?.For(Loc.Code) ?? Loc.T("Plugins.Settings.Title");
            if (groups.FirstOrDefault(g => g.Title == group) is not { Rows: not null } target)
            {
                target = (group, new List<UIElement>());
                groups.Add(target);
            }

            target.Rows.Add(_ui.Row(f.Title.For(Loc.Code), f.Hint?.For(Loc.Code), control));
        }

        foreach (var (title, rows) in groups)
        {
            _cards.Children.Add(_ui.Card(title, null, rows.ToArray()));
        }
    }

    /// <summary>Команды плагина: у каждой — своё сочетание и состояние (как на странице «Команды и сочетания»).</summary>
    private void AddPluginCommands(PluginManager pm, PluginManifest m)
    {
        if (m.Commands.Count == 0)
        {
            return;
        }

        var rows = new List<UIElement>();
        foreach (var c in m.Commands)
        {
            string full = $"plugin.{m.Id}.{c.Id}";
            if (_actions.Commands?.Get(full) is { } def && _prefs is not null)
            {
                rows.Add(_ui.Row(c.Title.For(Loc.Code), (c.Hold ? Loc.T("Plugins.Command.Hold") + " · " : "") + StatusText(full), CommandEditor(def)));
            }
            else
            {
                string? hk = _prefs?.Current.BindingsOrEmpty[full];
                rows.Add(_ui.Row(c.Title.For(Loc.Code), string.IsNullOrEmpty(hk) ? Loc.T("Plugins.Command.Off") : hk + " · " + Loc.T("Hotkeys.Status.Unavailable"),
                    new TextBlock()));
            }
        }

        _cards.Children.Add(_ui.Card(Loc.T("Plugins.Commands.Title"), Loc.T("Plugins.Commands.Hint"), rows.ToArray()));
    }

    // ── перенос встроенных инструментов ─────────────────────────────────────

    /// <summary>Поставляемые пакеты, которых нет среди установленных (или установлена более старая версия).</summary>
    internal static IReadOnlyList<(string Path, PluginManifest Manifest, string? Installed)> BundledToOffer(PluginManager pm)
    {
        // Phase 4H: только проверенные пакеты индекса поставки (bundled.json); при запуске они устанавливаются сами, поэтому
        // здесь остаются удалённые пользователем (их можно вернуть) и не установившиеся.
        var list = new List<(string, PluginManifest, string?)>();
        foreach (var e in BundledIndex.Load().Verified)
        {
            string path = e.Path;
            if (PluginStore.Inspect(path).Manifest is not { } m)
            {
                continue;
            }

            var have = pm.Store.Get(m.Id);
            if (have is null || CompareVersions(have.Manifest.Version, m.Version) < 0)
            {
                list.Add((path, m, have?.Manifest.Version));
            }
        }

        return list;
    }

    internal static int CompareVersions(string a, string b)
    {
        static Version V(string s) => Version.TryParse(s.Split('-')[0], out var v) ? v : new Version(0, 0);
        return V(a).CompareTo(V(b));
    }

    /// <summary>Карточка «Инструменты стали плагинами» на странице плагинов (если есть что импортировать).</summary>
    private void AddBundledCard(PluginManager pm)
    {
        var offer = BundledToOffer(pm);
        if (offer.Count == 0)
        {
            return;
        }

        var names = string.Join(", ", offer.Select(x => x.Manifest.Name.For(Loc.Code) + (x.Installed is { } v ? $" ({v} → {x.Manifest.Version})" : "")));
        var bar = new ButtonBar { Margin = new Thickness(0, 8, 0, 0) };
        bar.Children.Add(_ui.Button(ActionIcons.Import, Loc.T("Plugins.Bundled.Import"), () => ImportBundled(pm, offer), primary: true));
        _cards.Children.Insert(0, _ui.Card(Loc.T("Plugins.Bundled.Title"), Loc.T("Plugins.Bundled.Hint"), _ui.Note(names), bar));
    }

    /// <summary>
    /// Импорт поставляемых плагинов и перенос прежних настроек, сочетаний и «избранного» (правила — в манифесте). Плагины не
    /// включаются: первое включение по-прежнему требует подтверждения доверия.
    /// </summary>
    internal (int Imported, int Migrated) ImportBundled(PluginManager pm, IReadOnlyList<(string Path, PluginManifest Manifest, string? Installed)> offer, bool quiet = false)
    {
        int imported = 0, migrated = 0;
        foreach (var (path, _, _) in offer)
        {
            var r = pm.Import(path, BundledIndex.Sha256Of(path));
            if (r.Manifest is not { } m || r.Outcome is not (ImportOutcome.Installed or ImportOutcome.Updated))
            {
                continue;
            }

            imported++;
            if (_prefs is not null && MigrateLegacy(pm, m))
            {
                migrated++;
            }
        }

        if (!quiet)
        {
            KoDialog.Ask(this, Loc.T("Plugins.Bundled.Title"), Loc.F("Plugins.Bundled.Done", imported), [new("ok", Loc.T("Dialog.Ok"), true)]);
        }

        Rebuild();
        return (imported, migrated);
    }

    private bool MigrateLegacy(PluginManager pm, PluginManifest m) => PluginLegacyMigration.Apply(pm, _prefs!, _store, m);
}
