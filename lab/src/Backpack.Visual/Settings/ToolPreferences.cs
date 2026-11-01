using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Backpack.Visual.Windows;

namespace Backpack.Visual.Settings;

/// <summary>
/// Динамическая приватность (настройка компьютера, не профиля: импорт профиля её не меняет).
/// <see cref="BlockedApps"/> — имена исполняемых файлов (например, <c>keepass.exe</c>), для которых показ всегда закрыт.
/// </summary>
public sealed record PrivacySettings(bool Enabled = true, bool ShowBadge = true, IReadOnlyList<string>? BlockedApps = null)
{
    public static PrivacySettings Default { get; } = new();

    public IReadOnlyList<string> Apps => BlockedApps ?? [];

    public bool Equals(PrivacySettings? o) => o is not null && Enabled == o.Enabled && ShowBadge == o.ShowBadge && Apps.SequenceEqual(o.Apps);

    public override int GetHashCode() => HashCode.Combine(Enabled, ShowBadge, Apps.Count);

    /// <summary>Нормализация имени: только имя файла .exe в нижнем регистре; пустое или неверное — null.</summary>
    public static string? NormalizeApp(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string n = System.IO.Path.GetFileName(name.Trim().Trim('"')).ToLowerInvariant();
        if (!n.EndsWith(".exe", StringComparison.Ordinal))
        {
            n += ".exe";
        }

        return n.Length is > 4 and <= 128 && n.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) < 0 ? n : null;
    }
}

/// <summary>Инструменты включены (страница каждого инструмента начинается с этого переключателя). Выключение сохраняет настройки.</summary>
public sealed record ToolToggles(bool Keycaps = true, bool Focus = true, bool Drawing = true)
{
    public static ToolToggles Default { get; } = new();
}

/// <summary>Режим «Фокуса»: живое увеличение области или затемнение всего, кроме неё.</summary>
public enum FocusMode { Lens, Dim }

/// <summary>Готовые наборы оформления «Фокуса». Смена темы приложения их не перезаписывает.</summary>
public enum FocusPreset { Neutral, Emerald, Custom }

/// <summary>
/// «Фокус»: общий механизм выделения, перемещения и изменения размера области; два режима. Все размеры — DIP монитора
/// (значения концепта «px» трактуются как DIP WPF, а не как физические пиксели), время — мс, прозрачность — 0…1.
/// </summary>
public sealed record FocusSettings(
    FocusMode Mode = FocusMode.Lens,
    double Zoom = 1.5,
    double DimOpacity = 0.7,
    double Radius = 16,
    double BorderWidth = 2,
    string BorderColor = "#D4D4D4",
    double EdgeSoftness = 0,
    int AppearMs = 150,
    bool Shadow = true,
    bool Glow = false,
    FocusPreset Preset = FocusPreset.Neutral,
    MonitorMode DimMonitors = MonitorMode.Selected,
    double AreaWidth = 360,
    double AreaHeight = 220,
    bool ShowSource = true)
{
    /// <summary>Размер области, DIP, при выделении одним щелчком (без протягивания).</summary>
    public static readonly (double Min, double Max) AreaRange = (80, 1600);

    public static FocusSettings Default { get; } = new();

    public static readonly (double Min, double Max) ZoomRange = (1.0, 3.0);
    public static readonly (double Min, double Max) OpacityRange = (0.0, 0.95);
    public static readonly (double Min, double Max) RadiusRange = (0, 64);
    public static readonly (double Min, double Max) BorderRange = (0, 12);
    public static readonly (double Min, double Max) SoftRange = (0, 48);
    public static readonly (int Min, int Max) AppearRange = (0, 1000);

    /// <summary>Нейтральный набор по рекомендациям концепта: 70 %, 16, 2, #D4D4D4, мягкость 0, 150 мс.</summary>
    public FocusSettings WithNeutral() => this with { Preset = FocusPreset.Neutral, DimOpacity = 0.7, Radius = 16, BorderWidth = 2, BorderColor = "#D4D4D4", EdgeSoftness = 0, AppearMs = 150, Glow = false };

    /// <summary>Изумрудный набор со свечением (рамка #31C99A).</summary>
    public FocusSettings WithEmerald() => this with { Preset = FocusPreset.Emerald, DimOpacity = 0.72, Radius = 16, BorderWidth = 2, BorderColor = "#31C99A", EdgeSoftness = 0, AppearMs = 150, Glow = true };

    public FocusSettings Sanitized() => this with
    {
        Zoom = Clamp(Zoom, ZoomRange, 1.5),
        DimOpacity = Clamp(DimOpacity, OpacityRange, 0.7),
        Radius = Clamp(Radius, RadiusRange, 16),
        BorderWidth = Clamp(BorderWidth, BorderRange, 2),
        BorderColor = ThemeColor.TryParse(BorderColor, out _) ? BorderColor : "#D4D4D4",
        EdgeSoftness = Clamp(EdgeSoftness, SoftRange, 0),
        AppearMs = (int)Clamp(AppearMs, (AppearRange.Min, AppearRange.Max), 150),
        DimMonitors = DimMonitors is MonitorMode.Selected or MonitorMode.Multiple or MonitorMode.Primary ? DimMonitors : MonitorMode.Selected,
        AreaWidth = Clamp(AreaWidth, AreaRange, 360),
        AreaHeight = Clamp(AreaHeight, AreaRange, 220),
    };

    internal static double Clamp(double v, (double Min, double Max) r, double fallback) => double.IsFinite(v) ? Math.Clamp(v, r.Min, r.Max) : fallback;
}

/// <summary>Когда видно увеличение у курсора.</summary>
public enum CursorZoomMode
{
    /// <summary>Только пока удерживается назначенное сочетание.</summary>
    Hold,

    /// <summary>Следует за указателем, пока функция включена.</summary>
    Always,

    /// <summary>Сочетание создаёт область в текущей позиции; она остаётся там до отключения (изображение живое).</summary>
    Pinned,
}

public enum CursorZoomShape { Circle, RoundedRect }

/// <summary>Что делать со значком мыши у курсора, пока видно увеличение.</summary>
public enum CursorIconInteraction { KeepBeside, AutoHide }

/// <summary>
/// Увеличение у курсора (раздел «Мышь»). Размеры — DIP; <see cref="SmoothingMs"/> — постоянная времени сглаживания
/// следования (параметр поведения, не измеренная задержка захвата).
/// </summary>
public sealed record CursorZoomSettings(
    bool Enabled = false,
    CursorZoomMode Mode = CursorZoomMode.Hold,
    double Zoom = 1.5,
    double Diameter = 220,
    CursorZoomShape Shape = CursorZoomShape.Circle,
    int SmoothingMs = 50,
    double BorderWidth = 2,
    string BorderColor = "#31C99A",
    bool Shadow = true,
    CursorIconInteraction Icon = CursorIconInteraction.KeepBeside)
{
    public static CursorZoomSettings Default { get; } = new();

    public static readonly (double Min, double Max) ZoomRange = (1.1, 4.0);
    public static readonly (double Min, double Max) DiameterRange = (80, 600);
    public static readonly (int Min, int Max) SmoothingRange = (0, 300);

    public CursorZoomSettings Sanitized() => this with
    {
        Zoom = FocusSettings.Clamp(Zoom, ZoomRange, 1.5),
        Diameter = FocusSettings.Clamp(Diameter, DiameterRange, 220),
        SmoothingMs = (int)FocusSettings.Clamp(SmoothingMs, (SmoothingRange.Min, SmoothingRange.Max), 50),
        BorderWidth = FocusSettings.Clamp(BorderWidth, FocusSettings.BorderRange, 2),
        BorderColor = ThemeColor.TryParse(BorderColor, out _) ? BorderColor : "#31C99A",
    };
}

/// <summary>Нумерованные отметки и умный карандаш (поведение рисования на этом компьютере).</summary>
public sealed record MarkerSettings(int StartNumber = 1, double Size = 36, bool SmartPenConfirm = false)
{
    public static MarkerSettings Default { get; } = new();

    public static readonly (double Min, double Max) SizeRange = (20, 96);

    public MarkerSettings Sanitized() => this with
    {
        StartNumber = Math.Clamp(StartNumber, 0, 999),
        Size = FocusSettings.Clamp(Size, SizeRange, 36),
    };
}

/// <summary>
/// Назначенные сочетания команд реестра: id команды → текст сочетания (пустая строка — сочетание снято). Сравнение по значению.
/// Глобальные сочетания — настройка компьютера: импорт визуального профиля их не переносит.
/// </summary>
public sealed record CommandBindings(IReadOnlyDictionary<string, string> Map)
{
    public static CommandBindings Empty { get; } = new(new SortedDictionary<string, string>(StringComparer.Ordinal));

    public string? this[string id] => Map.TryGetValue(id, out var v) ? v : null;

    public CommandBindings With(string id, string? hotkey)
    {
        var d = new SortedDictionary<string, string>(Map.ToDictionary(x => x.Key, x => x.Value), StringComparer.Ordinal);
        if (hotkey is null)
        {
            d.Remove(id);
        }
        else
        {
            d[id] = hotkey;
        }

        return new CommandBindings(d);
    }

    public bool Equals(CommandBindings? o) => o is not null && Map.Count == o.Map.Count && Map.All(x => o.Map.TryGetValue(x.Key, out var v) && v == x.Value);

    public override int GetHashCode() => Map.Count;
}

/// <summary>Чтение и запись новых разделов app.json через System.Text.Json с последующей нормализацией значений.</summary>
public static class PreferenceSections
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
        WriteIndented = true,
        MaxDepth = 8,
    };

    public static JsonNode? ToNode<T>(T value) => JsonSerializer.SerializeToNode(value, Options);

    /// <summary>Раздел отсутствует или повреждён — значение по умолчанию; затем нормализация диапазонов.</summary>
    public static T Read<T>(JsonNode? node, T fallback, Func<T, T>? sanitize = null)
        where T : class
    {
        if (node is not JsonObject)
        {
            return fallback;
        }

        try
        {
            var v = node.Deserialize<T>(Options) ?? fallback;
            return sanitize is null ? v : sanitize(v);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or FormatException)
        {
            return fallback;
        }
    }

    /// <summary>Необязательный раздел: нет в файле — null (значения по умолчанию подставляет *OrDefault).</summary>
    public static T? ReadOptional<T>(JsonNode? node, T fallback, Func<T, T>? sanitize = null)
        where T : class => node is JsonObject ? Read(node, fallback, sanitize) : null;

    /// <summary>Назначения команд: id → сочетание (пустая строка — без сочетания). Неверные записи пропускаются.</summary>
    public static IReadOnlyDictionary<string, string> ReadBindings(JsonNode? node)
    {
        var d = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (node is JsonObject o)
        {
            foreach (var (k, v) in o.Take(256))
            {
                if (k.Length is > 0 and <= 96 && v is JsonValue jv && jv.TryGetValue(out string? text) && text.Length <= 64)
                {
                    d[k] = text;
                }
            }
        }

        return d;
    }
}
