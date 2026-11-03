using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Backpack.Sdk;

/// <summary>Версия SDK. Хост принимает плагин, если совпадает старший номер и младший не больше версии хоста.</summary>
public static class SdkInfo
{
    /// <summary>
    /// 1.1 (Phase 4G): инструменты увеличения и затемнения, которые рисует хост; удерживаемые команды; панель с кнопками.
    /// 1.2 (Phase 4H): холст рисования и индикатор, которые рисует хост; события смены раскладки; запись своих настроек;
    /// команды, действующие только пока открыт инструмент плагина; сочетание команды по умолчанию.
    /// 1.3: статичный снимок области экрана, который рисует хост (overlay.snapshot): выделение, захват в память, плавающее окно
    /// поверх остальных с временным развёртыванием; действия снимка (<see cref="SnapshotActions"/>).
    /// </summary>
    public const string Version = "1.3";

    /// <summary>Совместим ли плагин, собранный для <paramref name="pluginSdk"/>, с хостом версии <paramref name="hostSdk"/>.</summary>
    public static bool IsCompatible(string? pluginSdk, string hostSdk = Version)
    {
        if (!TryParse(pluginSdk, out var p) || !TryParse(hostSdk, out var h))
        {
            return false;
        }

        return p.Major == h.Major && p.Minor <= h.Minor;
    }

    public static bool TryParse(string? text, out (int Major, int Minor) v)
    {
        v = default;
        var parts = text?.Split('.') ?? [];
        if (parts.Length is < 1 or > 3 || !int.TryParse(parts[0], out int major) || major < 0)
        {
            return false;
        }

        int minor = 0;
        if (parts.Length > 1 && (!int.TryParse(parts[1], out minor) || minor < 0))
        {
            return false;
        }

        v = (major, minor);
        return true;
    }
}

/// <summary>Возможности, которые плагин заявляет в manifest. Хост проверяет их при каждом сообщении.</summary>
public static class Capabilities
{
    /// <summary>Команды в реестре Backpack (кнопки, меню, назначаемые сочетания).</summary>
    public const string Commands = "commands";

    /// <summary>Страница настроек, описанная схемой в manifest и нарисованная хостом.</summary>
    public const string Settings = "settings";

    /// <summary>Панель поверх экрана (заголовок, крупный текст, прогресс, кнопки), которую рисует хост.</summary>
    public const string OverlayPanel = "overlay.panel";

    /// <summary>Видимый ввод — только после фильтра приватности (скрытое приватностью не передаётся никогда).</summary>
    public const string InputVisible = "input.visible";

    /// <summary>Доступ к изображению экрана. Хост эту возможность не предоставляет.</summary>
    public const string Screen = "screen";

    /// <summary>
    /// Увеличение, которое рисует хост (SDK 1.1): следующее за указателем, закреплённое в месте вызова или перемещаемая линза с
    /// отдельной областью источника. Плагин задаёт только параметры; изображение экрана, координаты указателя и выбранной
    /// области остаются в Backpack и плагину не передаются.
    /// </summary>
    public const string OverlayMagnifier = "overlay.magnifier";

    /// <summary>Затемнение экрана вокруг выбранной пользователем области, которое рисует хост (SDK 1.1). Координаты плагину не передаются.</summary>
    public const string OverlayDim = "overlay.dim";

    /// <summary>
    /// Холст временного рисования, который рисует хост (SDK 1.2): панель инструментов и её режимы, радиальное меню, фигуры,
    /// умный карандаш, текст, номера, цвета, отмена и повтор, снимки экрана. Плагин задаёт параметры (<see cref="DrawRequest"/>)
    /// и выполняет действия (<see cref="DrawActions"/>); рисунки, указатель и снимок экрана плагину не передаются.
    /// </summary>
    public const string OverlayDraw = "overlay.draw";

    /// <summary>
    /// Индикатор поверх экрана (короткий код и вторая строка), который рисует хост (SDK 1.2): оформление — стиль индикатора или
    /// кейкапов из профиля, мониторы — выбор пользователя в Backpack, появление и исчезновение — по запросу.
    /// </summary>
    public const string OverlayIndicator = "overlay.indicator";

    /// <summary>
    /// События смены раскладки активного окна (SDK 1.2): код и название раскладки и причина (переключение в окне или смена окна).
    /// Нажатия клавиш сюда не входят. Запуск, разблокировка и выход из сна событием не считаются.
    /// </summary>
    public const string InputLayout = "input.layout";

    /// <summary>
    /// Статичный снимок области экрана, который рисует хост (SDK 1.3): пользователь выделяет область, хост копирует её в память и
    /// показывает в перемещаемом окне поверх остальных; двойной щелчок, Space над снимком или команда временно развёртывают его
    /// почти на весь экран. Изображение, координаты области и окна плагину не передаются и на диск не записываются.
    /// </summary>
    public const string OverlaySnapshot = "overlay.snapshot";

    public static readonly IReadOnlySet<string> Known = new HashSet<string>([Commands, Settings, OverlayPanel, InputVisible, Screen, OverlayMagnifier, OverlayDim,
        OverlayDraw, OverlayIndicator, InputLayout, OverlaySnapshot], StringComparer.Ordinal);
}

/// <summary>
/// Общие значения оформления инструментов, которые рисует хост, и плагинов от создателя: одно место для цвета рамки по
/// умолчанию «Фокуса», «Линзы» и «Снимка поверх окон» (изумрудный Backpack, как у увеличения у курсора). Константа
/// подставляется при компиляции — плагину с ней не нужна новая версия SDK во время работы.
/// </summary>
public static class ToolDefaults
{
    /// <summary>Цвет рамки по умолчанию (#RRGGBB).</summary>
    public const string BorderColor = "#31C99A";
}

/// <summary>
/// Параметры холста рисования (SDK 1.2). Строковые значения — имена из перечислений ниже; неизвестные хост заменяет значением по
/// умолчанию, числа ограничивает допустимыми диапазонами.
/// </summary>
/// <param name="Tool">select, smartpen, pen, line, arrow, rectangle, roundrect, ellipse, polygon, text, marker.</param>
/// <param name="Color">#RRGGBB или #RRGGBBAA.</param>
/// <param name="Dash">solid, dashed, dotted.</param>
/// <param name="FadeSec">Рисунки исчезают через столько секунд (0 — остаются до выхода).</param>
/// <param name="Toolbar">docked, autohide, radial (только радиальное меню).</param>
/// <param name="AutoHideEdge">top, bottom, left, right.</param>
/// <param name="ToolbarX">Сохранённое положение перетащенной панели (доля ширины экрана 0…1) или null.</param>
/// <param name="ShotAction">save, copy, both.</param>
/// <param name="AfterShot">keep, clear, exit.</param>
public sealed record DrawRequest(
    string Tool = "smartpen",
    string Color = "#00FF9C",
    double Thickness = 4,
    string Dash = "solid",
    int FadeSec = 0,
    double TextSize = 28,
    double CornerRadius = 16,
    int PolygonSides = 6,
    string Toolbar = "docked",
    string AutoHideEdge = "top",
    bool Compact = false,
    double? ToolbarX = null,
    double? ToolbarY = null,
    string ShotAction = "save",
    string AfterShot = "keep",
    int MarkerStart = 1,
    double MarkerSize = 36,
    bool AngleLabels = false);

/// <summary>Действия холста рисования (<see cref="IPluginContext.DrawAction"/>). Выбор инструмента — <c>tool:&lt;имя&gt;</c>.</summary>
public static class DrawActions
{
    public const string Undo = "undo", Redo = "redo", Clear = "clear", Screenshot = "screenshot", Radial = "radial", MarkersReset = "markers.reset";

    public static string Tool(string name) => "tool:" + name;
}

/// <summary>
/// Показ индикатора (SDK 1.2). <see cref="Animation"/>: inherit (как у кейкапов), fade, none. <see cref="Position"/>: overlay
/// (над группой кейкапов монитора) или screen (якорь <see cref="Anchor"/>: topLeft … bottomRight, center). <see cref="Style"/>:
/// keycaps (оформление кейкапов) или custom (стиль индикатора из профиля). Срок отсчитывается от показа и не продлевается
/// обновлением оформления.
/// </summary>
public sealed record IndicatorRequest(
    string Code,
    string? Detail = null,
    int DurationMs = 1200,
    string Animation = "inherit",
    string Position = "overlay",
    string Anchor = "center",
    double OffsetX = 0,
    double OffsetY = 0,
    string Style = "keycaps");

/// <summary>Смена раскладки (SDK 1.2). <see cref="Reason"/>: switch — в том же окне, window — другое окно с другой раскладкой.</summary>
public sealed record LayoutEvent(string Code, string? Detail, string Reason);

/// <summary>Вид увеличения.</summary>
public enum MagnifierMode
{
    /// <summary>Следует за указателем (сглаженно).</summary>
    Follow,

    /// <summary>Закреплено в месте, где указатель был в момент вызова; изображение живое.</summary>
    Pinned,

    /// <summary>Пользователь выделяет область; затем линза и область-источник перемещаются ручками независимо.</summary>
    Region,
}

/// <summary>Запрос увеличения (размеры — DIP монитора, цвет — #RRGGBB). Значения вне допустимых диапазонов хост ограничивает.</summary>
public sealed record MagnifierRequest(
    MagnifierMode Mode,
    double Zoom = 1.5,
    double Width = 220,
    double Height = 220,
    bool Round = false,
    double Radius = 16,
    double BorderWidth = 2,
    string BorderColor = ToolDefaults.BorderColor,
    bool Shadow = true,
    bool Glow = false,
    bool ShowSource = true,
    int SmoothingMs = 50,
    bool HideMouseIcon = false);

/// <summary>Запрос затемнения вокруг области. <see cref="Monitors"/>: selection, primary, all.</summary>
public sealed record DimRequest(
    double Opacity = 0.7,
    double Width = 360,
    double Height = 220,
    double Radius = 16,
    double BorderWidth = 2,
    string BorderColor = ToolDefaults.BorderColor,
    bool Glow = false,
    double Softness = 0,
    int AppearMs = 150,
    string Monitors = "selection");

/// <summary>
/// Снимок области экрана (SDK 1.3, overlay.snapshot). Размеры — DIP монитора, доли — 0…1, цвета — #RRGGBB. Неизвестные строки хост
/// заменяет значением по умолчанию, числа ограничивает. Оформление по умолчанию — как у «Фокуса» и «Линзы» (рамка <see cref="ToolDefaults.BorderColor"/>).
/// </summary>
/// <param name="Place">Где появляется новый снимок: last (на месте прежнего; в первый раз — в углу), corner (в углу), capture (поверх
/// снятой области, 1:1).</param>
/// <param name="Corner">topRight, topLeft, bottomRight, bottomLeft.</param>
/// <param name="StartSize">Длинная сторона нового снимка (DIP); больше исходного размера снимок при появлении не растягивается.</param>
/// <param name="SelectionDim">Слегка затемнять экран во время выделения.</param>
/// <param name="Aspect">keep (окно в пропорциях снимка), fit (свободное окно, снимок целиком на фоне <paramref name="Background"/>),
/// stretch (свободное окно, снимок растянут).</param>
/// <param name="MaxSize">Наибольший размер окна в обычном виде — доля рабочей области монитора.</param>
/// <param name="EdgeMargin">Отступ от края экрана при размещении в углу (DIP).</param>
/// <param name="Opacity">Непрозрачность окна в обычном виде (развёрнутый снимок всегда непрозрачен).</param>
/// <param name="SpaceHover">Space развёртывает и сворачивает снимок, пока указатель над ним (и не идёт ввод текста).</param>
/// <param name="AutoCollapse">Сворачивать по щелчку вне развёрнутого снимка или при переключении окон.</param>
/// <param name="ExpandMargin">Отступ развёрнутого снимка от краёв рабочей области (DIP).</param>
/// <param name="Upscale">Развёрнутый снимок может быть больше исходного размера.</param>
/// <param name="ClickThrough">Щелчки по снимку проходят к программе под ним (управление — ручками и кнопками при наведении).</param>
/// <param name="ExpandMonitor">Монитор развёрнутого снимка: snapshot (где снимок), pointer (под указателем), primary.</param>
/// <param name="KeepDip">При переносе на монитор с другим масштабом сохранять размер в DIP (иначе — в физических пикселях).</param>
/// <param name="Keep">session — снимок в памяти до выхода из Backpack; unlocked — также удаляется при блокировке и сне.</param>
public sealed record SnapshotRequest(
    string Place = "last",
    string Corner = "topRight",
    double StartSize = 420,
    bool SelectionDim = true,
    string Aspect = "keep",
    string Background = "#111513",
    double MinSize = 120,
    double MaxSize = 0.9,
    double EdgeMargin = 24,
    bool Topmost = true,
    double Opacity = 1,
    bool DoubleClick = true,
    bool SpaceHover = true,
    bool AutoCollapse = true,
    double ExpandMargin = 48,
    bool Upscale = true,
    bool ClickThrough = false,
    double Radius = 16,
    double BorderWidth = 2,
    string BorderColor = ToolDefaults.BorderColor,
    double BorderOpacity = 1,
    bool Glow = false,
    bool Shadow = true,
    string ShadowColor = "#000000",
    double ShadowBlur = 16,
    double ShadowOpacity = 0.55,
    string ExpandMonitor = "snapshot",
    bool KeepDip = true,
    string Keep = "session");

/// <summary>
/// Действия снимка (<see cref="IPluginContext.SnapshotAction"/>, SDK 1.3). Удалить снимок — <see cref="IPluginContext.CloseTool"/>;
/// показать ручки — <see cref="IPluginContext.EditTool"/>. <see cref="ResetPlace"/> действует и без открытого снимка.
/// </summary>
public static class SnapshotActions
{
    /// <summary>Выделить новую область; готовый снимок заменяет прежний (отмена Esc оставляет прежний).</summary>
    public const string Capture = "capture";

    public const string Show = "show", Hide = "hide", ToggleVisible = "toggleVisible";

    public const string Expand = "expand", Collapse = "collapse", ToggleExpand = "toggleExpand";

    /// <summary>Забыть сохранённое место и размер окна: снимок возвращается в угол по настройкам.</summary>
    public const string ResetPlace = "resetPlace";

    public static readonly IReadOnlySet<string> All = new HashSet<string>([Capture, Show, Hide, ToggleVisible, Expand, Collapse, ToggleExpand, ResetPlace], StringComparer.Ordinal);
}

/// <summary>Кнопка управления панели (иконка Lucide из набора хоста; неизвестная — заменяется). Появляется при наведении.</summary>
public sealed record PanelButton(string Id, string Icon, string Title);

/// <summary>
/// Панель, которую рисует хост. <see cref="State"/>: normal, warning, finished (ненавязчивое выделение). Кнопки, ручки
/// перемещения и изменения размера показываются только при наведении (единый механизм хоста); содержимое пропускает щелчки.
/// Положение и размер панели хранит хост.
/// </summary>
public sealed record PanelRequest(
    string Title,
    string Text,
    double? Progress = null,
    double FontSize = 30,
    double Opacity = 0.9,
    string? Accent = null,
    string State = "normal",
    IReadOnlyList<PanelButton>? Buttons = null,
    bool Movable = false);

/// <summary>Окружение плагина: что можно сделать через хост. Все вызовы потокобезопасны и не ждут ответа.</summary>
public interface IPluginContext
{
    /// <summary>Язык интерфейса Backpack: uk, en, ru.</summary>
    string Locale { get; }

    /// <summary>Текущие значения настроек (ключ из схемы manifest → значение). Хранятся в активном профиле Backpack.</summary>
    IReadOnlyDictionary<string, JsonNode?> Settings { get; }

    /// <summary>Показать или обновить панель (нужна возможность overlay.panel). <paramref name="progress"/> — 0…1 или null.</summary>
    void ShowPanel(string title, string text, double? progress = null);

    /// <summary>Панель с оформлением и кнопками (SDK 1.1).</summary>
    void ShowPanel(PanelRequest panel);

    void HidePanel();

    /// <summary>Открыть или обновить увеличение с идентификатором <paramref name="id"/> (нужна overlay.magnifier).</summary>
    void OpenMagnifier(string id, MagnifierRequest request);

    /// <summary>Открыть или обновить затемнение вокруг области (нужна overlay.dim). Область выделяет пользователь.</summary>
    void OpenDim(string id, DimRequest request);

    /// <summary>Закрыть инструмент (увеличение или затемнение).</summary>
    void CloseTool(string id);

    /// <summary>Показать управление инструментом или панелью (клавиатурный вызов редактирования) без наведения указателя.</summary>
    void EditTool(string id);

    /// <summary>Открыть холст рисования или обновить его параметры (нужна overlay.draw). Повтор с тем же ID не открывает второй.</summary>
    void OpenDrawing(string id, DrawRequest request);

    /// <summary>Действие на открытом холсте (<see cref="DrawActions"/>); без открытого холста ничего не делает.</summary>
    void DrawAction(string id, string action);

    /// <summary>Показать индикатор (нужна overlay.indicator); новый показ заменяет текущий и начинает срок заново.</summary>
    void ShowIndicator(IndicatorRequest request);

    void HideIndicator();

    /// <summary>
    /// Записать значения своих настроек (SDK 1.2; нужна settings): ключи и значения проверяются по схеме manifest, остальные
    /// поля не меняются. Например, запомнить последний цвет. <see cref="BackpackPlugin.OnSettingsChanged"/> не вызывается.
    /// </summary>
    void UpdateSettings(IReadOnlyDictionary<string, JsonNode?> values);

    /// <summary>
    /// Открыть снимок области экрана (SDK 1.3; нужна overlay.snapshot): новый ID начинает выделение области, повтор с тем же ID
    /// только обновляет параметры. Снимок закрывается <see cref="CloseTool"/> (изображение освобождается).
    /// </summary>
    void OpenSnapshot(string id, SnapshotRequest request);

    /// <summary>Действие снимка (<see cref="SnapshotActions"/>); неизвестный или закрытый снимок — ничего не происходит.</summary>
    void SnapshotAction(string id, string action);
}

/// <summary>
/// Базовый класс плагина. Хост вызывает методы в одном потоке, по очереди. Долгую работу выполняйте асинхронно:
/// методы должны возвращать управление быстро. Исключение в методе не роняет Backpack, но считается сбоем плагина.
/// </summary>
public abstract class BackpackPlugin
{
    protected IPluginContext Context { get; private set; } = null!;

    internal void Attach(IPluginContext context) => Context = context;

    /// <summary>Плагин включён: можно запускать таймеры и т. п.</summary>
    public virtual void OnStart()
    {
    }

    /// <summary>Выполнить команду из manifest (локальный ID, без префикса plugin.&lt;id&gt;.). Для команды с hold — нажатие.</summary>
    public virtual void OnCommand(string commandId)
    {
    }

    /// <summary>Команда с <c>"hold": true</c>: назначенное сочетание отпущено (или превышено время удержания).</summary>
    public virtual void OnCommandReleased(string commandId)
    {
    }

    /// <summary>
    /// Инструмент закрыт не плагином: <paramref name="reason"/> — escape (Esc), cancelled (отмена выделения), system
    /// (блокировка, смена рабочего стола, сон), host (выход Backpack); у снимка (SDK 1.3) также cleared (пользователь удалил снимок
    /// кнопкой) и unavailable (область не удалось снять).
    /// </summary>
    public virtual void OnToolClosed(string toolId, string reason)
    {
    }

    /// <summary>Нажата кнопка панели (<see cref="PanelButton.Id"/>).</summary>
    public virtual void OnPanelButton(string buttonId)
    {
    }

    /// <summary>Пользователь изменил настройки на странице, нарисованной хостом.</summary>
    public virtual void OnSettingsChanged()
    {
    }

    /// <summary>Видимое нажатие (только при возможности input.visible): подпись клавиши и Down/Up. Скрытое приватностью сюда не попадает.</summary>
    public virtual void OnVisibleKey(string key, bool down)
    {
    }

    /// <summary>Смена раскладки активного окна (только при возможности input.layout).</summary>
    public virtual void OnLayoutChanged(LayoutEvent e)
    {
    }

    /// <summary>
    /// Пользователь изменил параметры открытого инструмента на нём самом (SDK 1.2): у холста рисования — инструмент, цвет,
    /// толщина, штрих, положение панели и т. п. Ключи — как в <see cref="DrawRequest"/> (camelCase).
    /// </summary>
    public virtual void OnToolState(string toolId, IReadOnlyDictionary<string, JsonNode?> values)
    {
    }

    /// <summary>Плагин отключается: освободите ресурсы. После возврата процесс завершается; инструменты и панель хост закрывает сам.</summary>
    public virtual void OnStop()
    {
    }
}

/// <summary>Сообщения протокола (JSON, одна строка — одно сообщение). Поле «t» — тип.</summary>
public static class Protocol
{
    public const int MaxLine = 64 * 1024;

    // хост → плагин
    public const string Hello = "hello";
    public const string Command = "command";
    public const string SettingsChanged = "settings";
    public const string Key = "key";
    public const string Stop = "stop";
    public const string ToolClosed = "tool.closed";
    public const string PanelButtonPressed = "panel.button";
    public const string Layout = "layout";
    public const string ToolState = "tool.state";

    // плагин → хост
    public const string Ready = "ready";
    public const string Panel = "panel";
    public const string PanelHide = "panel.hide";
    public const string ToolOpen = "tool.open";
    public const string ToolClose = "tool.close";
    public const string ToolEdit = "tool.edit";
    public const string ToolAction = "tool.action";
    public const string Indicator = "indicator";
    public const string IndicatorHide = "indicator.hide";
    public const string SettingsUpdate = "settings.update";
    public const string Error = "error";

    public static string Write(JsonObject o) => o.ToJsonString(new JsonSerializerOptions { WriteIndented = false });

    /// <summary>Запрос увеличения → JSON (общий для плагина и хоста).</summary>
    public static JsonObject ToJson(string id, MagnifierRequest r) => new()
    {
        ["t"] = ToolOpen, ["id"] = id, ["kind"] = "magnifier", ["mode"] = r.Mode.ToString().ToLowerInvariant(),
        ["zoom"] = r.Zoom, ["width"] = r.Width, ["height"] = r.Height, ["round"] = r.Round, ["radius"] = r.Radius,
        ["borderWidth"] = r.BorderWidth, ["borderColor"] = r.BorderColor, ["shadow"] = r.Shadow, ["glow"] = r.Glow,
        ["showSource"] = r.ShowSource, ["smoothingMs"] = r.SmoothingMs, ["hideMouseIcon"] = r.HideMouseIcon,
    };

    public static JsonObject ToJson(string id, DimRequest r) => new()
    {
        ["t"] = ToolOpen, ["id"] = id, ["kind"] = "dim", ["opacity"] = r.Opacity, ["width"] = r.Width, ["height"] = r.Height,
        ["radius"] = r.Radius, ["borderWidth"] = r.BorderWidth, ["borderColor"] = r.BorderColor, ["glow"] = r.Glow,
        ["softness"] = r.Softness, ["appearMs"] = r.AppearMs, ["monitors"] = r.Monitors,
    };

    public static JsonObject ToJson(string id, DrawRequest r) => new()
    {
        ["t"] = ToolOpen, ["id"] = id, ["kind"] = "draw", ["tool"] = r.Tool, ["color"] = r.Color, ["thickness"] = r.Thickness, ["dash"] = r.Dash,
        ["fadeSec"] = r.FadeSec, ["textSize"] = r.TextSize, ["cornerRadius"] = r.CornerRadius, ["polygonSides"] = r.PolygonSides,
        ["toolbar"] = r.Toolbar, ["autoHideEdge"] = r.AutoHideEdge, ["compact"] = r.Compact, ["toolbarX"] = r.ToolbarX, ["toolbarY"] = r.ToolbarY,
        ["shotAction"] = r.ShotAction, ["afterShot"] = r.AfterShot, ["markerStart"] = r.MarkerStart, ["markerSize"] = r.MarkerSize,
        ["angleLabels"] = r.AngleLabels,
    };

    public static JsonObject ToJson(string id, SnapshotRequest r) => new()
    {
        ["t"] = ToolOpen, ["id"] = id, ["kind"] = "snapshot", ["place"] = r.Place, ["corner"] = r.Corner, ["startSize"] = r.StartSize,
        ["selectionDim"] = r.SelectionDim, ["aspect"] = r.Aspect, ["background"] = r.Background, ["minSize"] = r.MinSize, ["maxSize"] = r.MaxSize,
        ["edgeMargin"] = r.EdgeMargin, ["topmost"] = r.Topmost, ["opacity"] = r.Opacity, ["doubleClick"] = r.DoubleClick, ["spaceHover"] = r.SpaceHover,
        ["autoCollapse"] = r.AutoCollapse, ["expandMargin"] = r.ExpandMargin, ["upscale"] = r.Upscale, ["clickThrough"] = r.ClickThrough,
        ["radius"] = r.Radius, ["borderWidth"] = r.BorderWidth, ["borderColor"] = r.BorderColor, ["borderOpacity"] = r.BorderOpacity, ["glow"] = r.Glow,
        ["shadow"] = r.Shadow, ["shadowColor"] = r.ShadowColor, ["shadowBlur"] = r.ShadowBlur, ["shadowOpacity"] = r.ShadowOpacity,
        ["expandMonitor"] = r.ExpandMonitor, ["keepDip"] = r.KeepDip, ["keep"] = r.Keep,
    };

    public static JsonObject ToJson(IndicatorRequest r) => new()
    {
        ["t"] = Indicator, ["code"] = Trim(r.Code, 12), ["detail"] = r.Detail is { } d ? Trim(d, 80) : null, ["durationMs"] = r.DurationMs,
        ["animation"] = r.Animation, ["position"] = r.Position, ["anchor"] = r.Anchor, ["offsetX"] = r.OffsetX, ["offsetY"] = r.OffsetY, ["style"] = r.Style,
    };

    public static JsonObject ToJson(PanelRequest p) => new()
    {
        ["t"] = Panel, ["title"] = Trim(p.Title, 80), ["text"] = Trim(p.Text, 200), ["progress"] = p.Progress is { } v ? Math.Clamp(v, 0, 1) : null,
        ["fontSize"] = p.FontSize, ["opacity"] = p.Opacity, ["accent"] = p.Accent, ["state"] = p.State, ["movable"] = p.Movable,
        ["buttons"] = p.Buttons is { Count: > 0 } b ? new JsonArray(b.Take(6).Select(x => (JsonNode)new JsonObject { ["id"] = Trim(x.Id, 32), ["icon"] = Trim(x.Icon, 40), ["title"] = Trim(x.Title, 60) }).ToArray()) : null,
    };

    internal static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
}

/// <summary>Чтение значений настроек плагина (числа, флаги, строки) с запасным значением.</summary>
public static class SettingValues
{
    /// <remarks>Число может быть записано плагином как целое (<see cref="IPluginContext.UpdateSettings"/>): читается через текст JSON.</remarks>
    public static double Number(this IReadOnlyDictionary<string, JsonNode?> s, string key, double fallback) =>
        s.TryGetValue(key, out var v) && v is JsonValue jv && jv.GetValueKind() == JsonValueKind.Number
            ? jv.TryGetValue(out double d) ? d : double.Parse(jv.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture)
            : fallback;

    public static bool Flag(this IReadOnlyDictionary<string, JsonNode?> s, string key, bool fallback) =>
        s.TryGetValue(key, out var v) && v is JsonValue jv && jv.TryGetValue(out bool b) ? b : fallback;

    public static string Text(this IReadOnlyDictionary<string, JsonNode?> s, string key, string fallback) =>
        s.TryGetValue(key, out var v) && v is JsonValue jv && jv.TryGetValue(out string? t) && t is not null ? t : fallback;

    /// <summary>Строка времени mm:ss или h:mm:ss для отсчёта.</summary>
    public static string Clock(TimeSpan t) => t.ToString(t.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss", CultureInfo.InvariantCulture);
}

/// <summary>
/// Цикл сообщений плагина в процессе хоста (вызывается хостом Backpack, а не плагином). Читает команды из
/// <paramref name="input"/>, пишет ответы в <paramref name="output"/>. Ошибка в методе плагина сообщается хосту и
/// завершает цикл — хост учтёт сбой.
/// </summary>
public static class PluginRunner
{
    public static int Run(BackpackPlugin plugin, TextReader input, TextWriter output)
    {
        var context = new Context(output);
        plugin.Attach(context);
        string? line;
        bool started = false;
        try
        {
            while ((line = input.ReadLine()) is not null)
            {
                if (line.Length > Protocol.MaxLine || JsonNode.Parse(line) is not JsonObject msg)
                {
                    continue;
                }

                switch ((string?)msg["t"])
                {
                    case Protocol.Hello:
                        context.Locale = (string?)msg["locale"] ?? "en";
                        context.Replace(msg["settings"] as JsonObject);
                        context.Send(new JsonObject { ["t"] = Protocol.Ready, ["sdk"] = SdkInfo.Version });
                        plugin.OnStart();
                        started = true;
                        break;
                    case Protocol.Command when started:
                        if ((string?)msg["phase"] == "up")
                        {
                            plugin.OnCommandReleased((string?)msg["id"] ?? "");
                        }
                        else
                        {
                            plugin.OnCommand((string?)msg["id"] ?? "");
                        }

                        break;
                    case Protocol.ToolClosed when started:
                        plugin.OnToolClosed((string?)msg["id"] ?? "", (string?)msg["reason"] ?? "host");
                        break;
                    case Protocol.PanelButtonPressed when started:
                        plugin.OnPanelButton((string?)msg["id"] ?? "");
                        break;
                    case Protocol.SettingsChanged when started:
                        context.Replace(msg["values"] as JsonObject);
                        plugin.OnSettingsChanged();
                        break;
                    case Protocol.Key when started:
                        plugin.OnVisibleKey((string?)msg["key"] ?? "", (bool?)msg["down"] ?? false);
                        break;
                    case Protocol.Layout when started:
                        plugin.OnLayoutChanged(new LayoutEvent((string?)msg["code"] ?? "", (string?)msg["detail"], (string?)msg["reason"] ?? "switch"));
                        break;
                    case Protocol.ToolState when started:
                        plugin.OnToolState((string?)msg["id"] ?? "",
                            (msg["values"] as JsonObject)?.ToDictionary(x => x.Key, x => x.Value?.DeepClone()) ?? new Dictionary<string, JsonNode?>());
                        break;
                    case Protocol.Stop:
                        plugin.OnStop();
                        return 0;
                }
            }
        }
        catch (Exception ex)
        {
            context.Send(new JsonObject { ["t"] = Protocol.Error, ["message"] = ex.GetType().Name });
            return 3;
        }

        if (started)
        {
            plugin.OnStop();
        }

        return 0;
    }

    private sealed class Context(TextWriter output) : IPluginContext
    {
        private readonly Lock _lock = new();
        private Dictionary<string, JsonNode?> _settings = new();

        public string Locale { get; set; } = "en";

        public IReadOnlyDictionary<string, JsonNode?> Settings => _settings;

        public void Replace(JsonObject? values) =>
            _settings = values?.ToDictionary(x => x.Key, x => x.Value?.DeepClone()) ?? new Dictionary<string, JsonNode?>();

        public void ShowPanel(string title, string text, double? progress = null) => ShowPanel(new PanelRequest(title, text, progress));

        public void ShowPanel(PanelRequest panel) => Send(Protocol.ToJson(panel));

        public void HidePanel() => Send(new JsonObject { ["t"] = Protocol.PanelHide });

        public void OpenMagnifier(string id, MagnifierRequest request) => Send(Protocol.ToJson(id, request));

        public void OpenDim(string id, DimRequest request) => Send(Protocol.ToJson(id, request));

        public void CloseTool(string id) => Send(new JsonObject { ["t"] = Protocol.ToolClose, ["id"] = id });

        public void EditTool(string id) => Send(new JsonObject { ["t"] = Protocol.ToolEdit, ["id"] = id });

        public void OpenDrawing(string id, DrawRequest request) => Send(Protocol.ToJson(id, request));

        public void DrawAction(string id, string action) => Send(new JsonObject { ["t"] = Protocol.ToolAction, ["id"] = id, ["action"] = Protocol.Trim(action, 48) });

        public void ShowIndicator(IndicatorRequest request) => Send(Protocol.ToJson(request));

        public void HideIndicator() => Send(new JsonObject { ["t"] = Protocol.IndicatorHide });

        public void OpenSnapshot(string id, SnapshotRequest request) => Send(Protocol.ToJson(id, request));

        public void SnapshotAction(string id, string action) => Send(new JsonObject { ["t"] = Protocol.ToolAction, ["id"] = id, ["action"] = Protocol.Trim(action, 48) });

        public void UpdateSettings(IReadOnlyDictionary<string, JsonNode?> values)
        {
            var o = new JsonObject();
            foreach (var (k, v) in values)
            {
                o[k] = v?.DeepClone();
                _settings[k] = v?.DeepClone();
            }

            Send(new JsonObject { ["t"] = Protocol.SettingsUpdate, ["values"] = o });
        }

        public void Send(JsonObject o)
        {
            lock (_lock)
            {
                output.WriteLine(Protocol.Write(o));
                output.Flush();
            }
        }
    }
}
