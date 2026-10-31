using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Plugins;

/// <summary>Что перенести в плагин из прежних встроенных инструментов.</summary>
/// <param name="Settings">Значения настроек плагина (уже по схеме manifest) или null — переносить нечего.</param>
/// <param name="Bindings">Новые назначения: полный ID команды плагина → сочетание.</param>
/// <param name="Removed">Прежние ID команд, назначения которых больше не нужны.</param>
/// <param name="Favorite">Прежний инструмент был включён — добавить плагин в меню.</param>
public sealed record MigrationPlan(JsonObject? Settings, IReadOnlyDictionary<string, string> Bindings, IReadOnlyList<string> Removed, bool Favorite)
{
    public bool IsEmpty => Settings is null && Bindings.Count == 0 && !Favorite;
}

/// <summary>
/// Перенос настроек, сочетаний и «избранного» прежних встроенных инструментов в плагины (Phase 4G). Правила — в блоке
/// <c>legacy</c> манифеста (<see cref="PluginLegacy"/>), поэтому хост не проверяет ID конкретных плагинов:
/// <list type="bullet">
/// <item><c>settings</c>: ключ плагина → поле прежнего раздела; суффикс <c>*k</c> умножает число (например, <c>dimOpacity*100</c>);</item>
/// <item><c>commands</c>: прежний ID → команда плагина; суффикс <c>|поле=значение</c> — только если поле раздела имеет это
///       значение (прежняя общая команда «Фокуса» уходила в выбранный режим);</item>
/// <item><c>tool</c>: путь <c>раздел.поле</c> к прежнему переключателю: true — плагин добавляется в меню.</item>
/// </list>
/// Перенос не включает плагин и не подтверждает доверие: это по-прежнему делает пользователь.
/// </summary>
public static class PluginMigration
{
    /// <param name="legacy">Прежние разделы предпочтений (camelCase, как в app.json; значения по умолчанию заполнены).</param>
    /// <param name="hotkeyOf">Действующее сочетание прежней команды (с учётом прежнего значения по умолчанию); null — нет.</param>
    /// <param name="existing">Уже сохранённые значения плагина в профиле: тогда настройки не переносятся повторно.</param>
    /// <param name="bound">Команда плагина уже имеет назначение — не перезаписывать.</param>
    public static MigrationPlan Plan(PluginManifest m, JsonObject legacy, Func<string, string?> hotkeyOf, JsonObject? existing, Func<string, bool> bound)
    {
        if (m.Legacy is not { } lg)
        {
            return new MigrationPlan(null, new Dictionary<string, string>(), [], false);
        }

        var section = lg.Section is { } sec ? legacy[sec] as JsonObject : null;
        JsonObject? settings = null;
        if (section is not null && (existing is null || existing.Count == 0))
        {
            foreach (var (key, source) in lg.Settings)
            {
                if (m.Settings.FirstOrDefault(f => f.Key == key) is not { } f)
                {
                    continue;
                }

                string field = source;
                double factor = 1;
                int star = source.IndexOf('*');
                if (star > 0 && double.TryParse(source[(star + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out double k))
                {
                    field = source[..star];
                    factor = k;
                }

                var value = section[field];
                if (factor != 1 && value is JsonValue jv && jv.TryGetValue(out double d))
                {
                    value = JsonValue.Create(Math.Round(d * factor, 6));
                }

                if (PluginStore.Sanitize(f, value?.DeepClone()) is { } clean)
                {
                    settings ??= new JsonObject();
                    settings[key] = clean;
                }
            }
        }

        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        var removed = new List<string>();
        foreach (var (oldId, target) in lg.Commands)
        {
            string[] parts = target.Split('|', 2);
            if (parts.Length == 2 && !Matches(section, parts[1]))
            {
                continue;
            }

            string full = $"plugin.{m.Id}.{parts[0]}";
            removed.Add(oldId);
            // Пустая строка — сочетание было снято пользователем: переносится как «без сочетания», чтобы не включилось
            // сочетание плагина по умолчанию (manifest, SDK 1.2).
            if (hotkeyOf(oldId) is { } hk && !bound(full) && !bindings.ContainsKey(full))
            {
                bindings[full] = hk;
            }
        }

        bool favorite = lg.Tool is { } path && Path(legacy, path) is JsonValue tv && tv.TryGetValue(out bool on) && on;
        return new MigrationPlan(settings, bindings, removed, favorite);
    }

    private static bool Matches(JsonObject? section, string condition)
    {
        int eq = condition.IndexOf('=');
        return eq > 0 && section?[condition[..eq]] is JsonValue v && v.TryGetValue(out string? s) && string.Equals(s, condition[(eq + 1)..], StringComparison.OrdinalIgnoreCase);
    }

    private static JsonNode? Path(JsonObject root, string path)
    {
        JsonNode? n = root;
        foreach (var part in path.Split('.'))
        {
            n = (n as JsonObject)?[part];
        }

        return n;
    }

    /// <summary>
    /// Синхронизация поставляемых плагинов при запуске (Phase 4H): каждый проверенный пакет индекса поставки, которого нет среди
    /// установленных (или установлен более старой версии, или той же версии без отметки), устанавливается. Код не запускается;
    /// новые плагины остаются выключенными, у обновляемых сохраняется состояние включения. Удалённые пользователем
    /// поставляемые плагины не возвращаются. Возвращает установленные впервые (для переноса настроек и уведомления).
    /// </summary>
    public static IReadOnlyList<PluginManifest> SyncBundled(PluginStore store, BundledIndex index)
    {
        var added = new List<PluginManifest>();
        foreach (var e in index.Verified)
        {
            if (store.DismissedBundled.Contains(e.Id))
            {
                continue;
            }

            var have = store.Get(e.Id);
            bool needed = have is null || PluginManifest.CompareVersions(have.Manifest.Version, e.Version) < 0
                || (PluginManifest.CompareVersions(have.Manifest.Version, e.Version) == 0 && !store.IsBundled(have));
            if (needed && store.Import(e.Path, e.Sha256) is { Outcome: ImportOutcome.Installed or ImportOutcome.Updated, Manifest: { } m } && have is null)
            {
                added.Add(m);
            }
        }

        return added;
    }

    /// <summary>Пакеты, поставляемые вместе с Backpack (папка Plugins рядом с программой); ничего не запускается.</summary>
    public static string BundledDirectory() =>
        Environment.GetEnvironmentVariable("BACKPACK_BUNDLED_PLUGINS") is { Length: > 0 } env ? env : System.IO.Path.Combine(AppContext.BaseDirectory, "Plugins");

    public static IReadOnlyList<string> BundledPackages(string? dir = null)
    {
        dir ??= BundledDirectory();
        try
        {
            return Directory.Exists(dir) ? Directory.GetFiles(dir, "*" + PluginStore.PackageExtension).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList() : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
