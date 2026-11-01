namespace Backpack.Visual.Settings;

/// <summary>
/// Шрифты (settings v6, входят в профиль): интерфейс всех окон и подписи на кейкапах — независимо. <c>null</c> — встроенный Inter.
/// Имя — семейство шрифта Windows. Если на этом компьютере семейства нет, используется встроенный Inter (предсказуемая замена),
/// а страница «Внешний вид» сообщает, что выбранный шрифт недоступен. Символы, которых нет в шрифте, WPF берёт из Inter и
/// Segoe UI (цепочка запасных семейств), поэтому кириллица и греческий не пропадают.
/// </summary>
public sealed record FontSettings(string? Ui = null, string? Keycap = null)
{
    public static FontSettings Default { get; } = new();

    public const int MaxNameLength = 96;

    /// <summary>Встроенный шрифт Spectral (Production Type, OFL 1.1) — единственный вариант кроме «Стандартного» (null).</summary>
    public const string Spectral = "Spectral";

    /// <summary>Варианты выбора (Phase 4F): null — «Стандартный» (штатный шрифт области), <see cref="Spectral"/>.</summary>
    public static IReadOnlyList<string?> Choices { get; } = [null, Spectral];

    /// <summary>
    /// Значение из файла: «Spectral» (без учёта регистра) или null. Любое другое семейство (прежние версии позволяли выбрать
    /// установленный шрифт) безопасно переводится на «Стандартный».
    /// </summary>
    public static string? NormalizeName(string? name) =>
        name is not null && string.Equals(name.Trim(), Spectral, StringComparison.OrdinalIgnoreCase) ? Spectral : null;

    public FontSettings Sanitized() => new(NormalizeName(Ui), NormalizeName(Keycap));
}

/// <summary>Дополнительная подпись раскладки в левом верхнем углу кейкапа.</summary>
public enum SecondaryLabelMode
{
    /// <summary>Не показывать.</summary>
    Off,

    /// <summary>Символ активной раскладки целевого приложения (если он отличается от основной английской подписи).</summary>
    ActiveLayout,

    /// <summary>Символ выбранной установленной раскладки — всегда, независимо от активной.</summary>
    FixedLayout,
}

/// <summary>Угол кейкапа для дополнительной подписи раскладки (settings v8).</summary>
public enum LabelCorner { TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>
/// Дополнительные подписи раскладки (settings v6, входят в профиль): что показывать. <see cref="Layout"/> — идентификатор
/// раскладки Windows (KLID, восемь шестнадцатеричных цифр, например <c>00000422</c> — украинская) для режима
/// <see cref="SecondaryLabelMode.FixedLayout"/>. Геометрия двух подписей (settings v9) — часть вида кейкапа:
/// <see cref="TwoLabelLayout"/> в <see cref="AppearanceSettings.TwoLabels"/>, поэтому хранится и в стилях кейкапов, и в профиле.
/// </summary>
public sealed record SecondaryLabelSettings(
    SecondaryLabelMode Mode = SecondaryLabelMode.Off,
    string? Layout = null)
{
    public static SecondaryLabelSettings Default { get; } = new();

    public static bool IsValidLayoutId(string? id) => id is { Length: 8 } && id.All(Uri.IsHexDigit);

    public SecondaryLabelSettings Sanitized() => this with
    {
        Layout = IsValidLayoutId(Layout) ? Layout!.ToUpperInvariant() : null,
        Mode = Mode == SecondaryLabelMode.FixedLayout && !IsValidLayoutId(Layout) ? SecondaryLabelMode.ActiveLayout : Mode,
    };
}

/// <summary>
/// Геометрия двух подписей на кейкапе (settings v9, Phase 4H). Размеры — доли стандартного размера подписи кейкапа, который
/// пропорционален лицевой поверхности (поэтому настройки сохраняются при изменении общего размера):
/// <see cref="MainScale"/> — основная (английская) подпись, <see cref="SecondaryScale"/> — дополнительная.
/// Место дополнительной: угол <see cref="Corner"/> лицевой поверхности; начало координат — безопасная точка у этого угла
/// (отступ от края с учётом скругления); <see cref="OffsetX"/>/<see cref="OffsetY"/> — сдвиг от неё в долях ширины и высоты
/// грани, положительный — к центру грани. Обе подписи всегда остаются внутри грани.
/// </summary>
public sealed record TwoLabelLayout(
    double MainScale = 0.78,
    double SecondaryScale = 0.42,
    LabelCorner Corner = LabelCorner.BottomRight,
    double OffsetX = 0,
    double OffsetY = 0)
{
    public static TwoLabelLayout Default { get; } = new();

    public static readonly (double Min, double Max) MainRange = (0.5, 1.0);

    public static readonly (double Min, double Max) SecondaryRange = (0.25, 0.7);

    public static readonly (double Min, double Max) OffsetRange = (-0.15, 0.35);

    public TwoLabelLayout Sanitized() => this with
    {
        MainScale = Clamp(MainScale, MainRange, Default.MainScale),
        SecondaryScale = Clamp(SecondaryScale, SecondaryRange, Default.SecondaryScale),
        OffsetX = Clamp(OffsetX, OffsetRange, 0),
        OffsetY = Clamp(OffsetY, OffsetRange, 0),
    };

    private static double Clamp(double v, (double Min, double Max) range, double fallback) =>
        double.IsFinite(v) ? Math.Clamp(v, range.Min, range.Max) : fallback;
}

/// <summary>
/// Настройки плагинов в профиле (settings v8): ID плагина → объект значений. Значения проверяются по схеме манифеста при
/// чтении плагином (<see cref="Plugins.PluginStore.Sanitize"/>), поэтому здесь хранится только JSON-объект не больше
/// <see cref="MaxBytes"/>. Сравнение — по содержимому (профили сравниваются по значениям).
/// </summary>
public sealed class PluginProfileValues : IEquatable<PluginProfileValues>
{
    public const int MaxPlugins = 64;
    public const int MaxBytes = 16 * 1024;

    private readonly SortedDictionary<string, string> _map;

    public PluginProfileValues(IEnumerable<KeyValuePair<string, string>>? map = null)
    {
        _map = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in map ?? [])
        {
            if (_map.Count < MaxPlugins && k.Length is > 0 and <= 64 && v.Length <= MaxBytes)
            {
                _map[k] = v;
            }
        }
    }

    public static PluginProfileValues Empty { get; } = new();

    public IReadOnlyDictionary<string, string> Map => _map;

    public System.Text.Json.Nodes.JsonObject? Get(string id)
    {
        try
        {
            return _map.TryGetValue(id, out var json) ? System.Text.Json.Nodes.JsonNode.Parse(json) as System.Text.Json.Nodes.JsonObject : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public PluginProfileValues With(string id, System.Text.Json.Nodes.JsonObject? values)
    {
        var next = new Dictionary<string, string>(_map, StringComparer.Ordinal);
        if (values is null)
        {
            next.Remove(id);
        }
        else
        {
            next[id] = values.ToJsonString();
        }

        return new PluginProfileValues(next);
    }

    public bool Equals(PluginProfileValues? other) => other is not null && _map.Count == other._map.Count && _map.All(x => other._map.TryGetValue(x.Key, out var v) && v == x.Value);

    public override bool Equals(object? obj) => Equals(obj as PluginProfileValues);

    public override int GetHashCode() => _map.Count;
}
