namespace KeyOverlay.Visual.Rendering;

/// <summary>Набор обозначений. Меняет только подписи и символы, не назначение физических клавиш и не ввод.</summary>
public enum LabelSet { Windows, MacOS, Linux }

/// <summary>Текст — только подписи; символы — символ, если он есть, иначе короткая подпись; смешанный — символ над подписью.</summary>
public enum LabelMode { Text, Symbol, Mixed }

public enum KeySide { None, Left, Right }

/// <summary>Как нарисовать содержимое кейкапа.</summary>
public enum CapLayout
{
    /// <summary>Символьная клавиша (буква, цифра, знак, F-клавиша): одна крупная подпись.</summary>
    Character,
    /// <summary>Только символ, крупно (стрелки, пробел; в режиме «символы» — все клавиши с символом).</summary>
    GlyphOnly,
    /// <summary>Символ сверху, короткая подпись снизу (как у Keyviz «короткий текст + иконка»).</summary>
    GlyphAndText,
    /// <summary>Только короткая подпись служебной клавиши, уменьшенным шрифтом.</summary>
    TextOnly,
    /// <summary>Кнопка мыши: значок мыши с подсвеченной кнопкой.</summary>
    MouseGlyph,
}

/// <param name="Text">Подпись (для GlyphOnly — запасная, для озвучивания и тестов).</param>
/// <param name="Marker">Мелкая угловая метка: «R» у правых модификаторов, «num» у цифрового блока, «?» у неизвестного состояния.</param>
public sealed record KeyCapContent(CapLayout Layout, string Text, Glyph Glyph, string? Marker, MouseButtons MouseButton = MouseButtons.None);

/// <summary>
/// Явная таблица обозначений KeyOverlay (собственные данные, не таблица Keyviz). Ключ — семантическое имя физической
/// клавиши (<see cref="KeycapItem.Key"/>), которое не зависит от набора обозначений.
/// Правила запасного варианта: нет символа → короткая подпись; нет записи в таблице → подпись как есть
/// (до 3 символов — крупно, длиннее — уменьшенным шрифтом).
/// </summary>
public static class KeyLabels
{
    /// <param name="Text">Подпись для текстового режима, если короткая подпись под символом неоднозначна без символа.</param>
    private sealed record Entry(string Short, string Full, Glyph Glyph, bool Modifier = false, string? Text = null);

    private static readonly Dictionary<string, Entry> Windows = new()
    {
        ["Ctrl"] = new("Ctrl", "Control", Glyph.None, true),
        ["Shift"] = new("Shift", "Shift", Glyph.Shift, true),
        ["Alt"] = new("Alt", "Alt", Glyph.None, true),
        ["Win"] = new("Win", "Windows", Glyph.WindowsGrid, true),
        ["Enter"] = new("Enter", "Enter", Glyph.Enter),
        ["Backspace"] = new("Bksp", "Backspace", Glyph.Backspace),
        ["Delete"] = new("Del", "Delete", Glyph.None),
        ["Insert"] = new("Ins", "Insert", Glyph.None),
        ["Tab"] = new("Tab", "Tab", Glyph.Tab),
        ["Esc"] = new("Esc", "Escape", Glyph.None),
        ["Space"] = new("Space", "Space", Glyph.Space),
        ["CapsLock"] = new("Caps", "Caps Lock", Glyph.CapsLock),
        ["Home"] = new("Home", "Home", Glyph.None),
        ["End"] = new("End", "End", Glyph.None),
        ["PageUp"] = new("PgUp", "Page Up", Glyph.None),
        ["PageDown"] = new("PgDn", "Page Down", Glyph.None),
        ["PrintScreen"] = new("PrtSc", "Print Screen", Glyph.None),
        ["Menu"] = new("Menu", "Menu", Glyph.Menu),
        ["Pause"] = new("Pause", "Pause", Glyph.None),
        ["NumLock"] = new("NumLk", "Num Lock", Glyph.None),
        ["ScrollLock"] = new("ScrLk", "Scroll Lock", Glyph.None),
    };

    private static readonly Dictionary<string, Entry> MacOS = new()
    {
        ["Ctrl"] = new("Ctrl", "Control", Glyph.Control, true),
        ["Shift"] = new("Shift", "Shift", Glyph.Shift, true),
        ["Alt"] = new("Opt", "Option", Glyph.Option, true),
        ["Win"] = new("Cmd", "Command", Glyph.Command, true),
        ["Enter"] = new("Return", "Return", Glyph.Return),
        ["Backspace"] = new("Del", "Delete", Glyph.Backspace, Text: "Delete"),
        ["Delete"] = new("Del", "Forward Delete", Glyph.ForwardDelete, Text: "Fwd Del"),
        ["Insert"] = new("Ins", "Insert", Glyph.None),
        ["Tab"] = new("Tab", "Tab", Glyph.Tab),
        ["Esc"] = new("Esc", "Escape", Glyph.Escape),
        ["Space"] = new("Space", "Space", Glyph.Space),
        ["CapsLock"] = new("Caps", "Caps Lock", Glyph.CapsLock),
        ["Home"] = new("Home", "Home", Glyph.Home),
        ["End"] = new("End", "End", Glyph.End),
        ["PageUp"] = new("PgUp", "Page Up", Glyph.PageUp),
        ["PageDown"] = new("PgDn", "Page Down", Glyph.PageDown),
        ["PrintScreen"] = new("PrtSc", "Print Screen", Glyph.None),
        ["Menu"] = new("Menu", "Menu", Glyph.Menu),
        ["Pause"] = new("Pause", "Pause", Glyph.None),
        ["NumLock"] = new("Clear", "Clear", Glyph.None),
        ["ScrollLock"] = new("ScrLk", "Scroll Lock", Glyph.None),
    };

    private static readonly Dictionary<string, Entry> Linux = new()
    {
        ["Ctrl"] = new("Ctrl", "Control", Glyph.None, true),
        ["Shift"] = new("Shift", "Shift", Glyph.Shift, true),
        ["Alt"] = new("Alt", "Alt", Glyph.None, true),
        ["Win"] = new("Super", "Super", Glyph.SuperDiamond, true),
        ["Enter"] = new("Enter", "Enter", Glyph.Enter),
        ["Backspace"] = new("Bksp", "Backspace", Glyph.Backspace),
        ["Delete"] = new("Del", "Delete", Glyph.None),
        ["Insert"] = new("Ins", "Insert", Glyph.None),
        ["Tab"] = new("Tab", "Tab", Glyph.Tab),
        ["Esc"] = new("Esc", "Escape", Glyph.None),
        ["Space"] = new("Space", "Space", Glyph.Space),
        ["CapsLock"] = new("Caps", "Caps Lock", Glyph.CapsLock),
        ["Home"] = new("Home", "Home", Glyph.None),
        ["End"] = new("End", "End", Glyph.None),
        ["PageUp"] = new("PgUp", "Page Up", Glyph.None),
        ["PageDown"] = new("PgDn", "Page Down", Glyph.None),
        ["PrintScreen"] = new("PrtSc", "Print", Glyph.None),
        ["Menu"] = new("Menu", "Menu", Glyph.Menu),
        ["Pause"] = new("Pause", "Pause", Glyph.None),
        ["NumLock"] = new("NumLk", "Num Lock", Glyph.None),
        ["ScrollLock"] = new("ScrLk", "Scroll Lock", Glyph.None),
    };

    /// <summary>
    /// Мультимедиа и клавиши запуска — одинаково во всех наборах обозначений. Подписи — что делает клавиша (Fn до Windows не
    /// доходит: прошивка присылает уже результат, например Mute вместо «Fn+F1»).
    /// </summary>
    private static readonly Dictionary<string, Entry> Media = new()
    {
        ["VolumeMute"] = new("Mute", "Mute", Glyph.VolumeMute),
        ["VolumeDown"] = new("Vol −", "Volume down", Glyph.VolumeDown),
        ["VolumeUp"] = new("Vol +", "Volume up", Glyph.VolumeUp),
        ["MediaPlayPause"] = new("Play", "Play / Pause", Glyph.PlayPause),
        ["MediaStop"] = new("Stop", "Stop", Glyph.MediaStop),
        ["MediaNext"] = new("Next", "Next track", Glyph.NextTrack),
        ["MediaPrev"] = new("Prev", "Previous track", Glyph.PrevTrack),
        ["MediaSelect"] = new("Media", "Media", Glyph.None),
        ["LaunchApp1"] = new("PC", "This PC", Glyph.None),
        ["LaunchApp2"] = new("Calc", "Calculator", Glyph.None),
        ["LaunchMail"] = new("Mail", "Mail", Glyph.None),
        ["BrowserHome"] = new("Web", "Browser home", Glyph.None),
        ["BrowserSearch"] = new("Search", "Search", Glyph.None),
        ["BrowserFavorites"] = new("Favs", "Favorites", Glyph.None),
        ["BrowserRefresh"] = new("Refresh", "Refresh", Glyph.None),
        ["BrowserStop"] = new("Stop", "Stop loading", Glyph.None),
        ["BrowserForward"] = new("Fwd", "Forward", Glyph.None),
        ["BrowserBack"] = new("Back", "Back", Glyph.None),
        ["Power"] = new("Power", "Power", Glyph.None),
        ["Sleep"] = new("Sleep", "Sleep", Glyph.None),
        ["Wake"] = new("Wake", "Wake", Glyph.None),
    };

    public static IReadOnlyCollection<string> MediaKeys => Media.Keys;

    private static readonly Dictionary<string, (string Text, Glyph Glyph)> Arrows = new()
    {
        ["ArrowUp"] = ("Up", Glyph.ArrowUp),
        ["ArrowDown"] = ("Down", Glyph.ArrowDown),
        ["ArrowLeft"] = ("Left", Glyph.ArrowLeft),
        ["ArrowRight"] = ("Right", Glyph.ArrowRight),
    };

    private static readonly Dictionary<string, (string Text, MouseButtons Button)> Mouse = new()
    {
        ["MouseLeft"] = ("Left", MouseButtons.Left),
        ["MouseRight"] = ("Right", MouseButtons.Right),
        ["MouseMiddle"] = ("Middle", MouseButtons.Middle),
        ["MouseX1"] = ("Back", MouseButtons.X1),
        ["MouseX2"] = ("Fwd", MouseButtons.X2),
    };

    public static IReadOnlyCollection<string> SpecialKeys => Windows.Keys;

    public static IReadOnlyCollection<string> ArrowKeys => Arrows.Keys;

    public static IReadOnlyCollection<string> MouseKeys => Mouse.Keys;

    public static bool IsMouse(string key) => Mouse.ContainsKey(key);

    public static bool IsModifierKey(string key) => key is "Ctrl" or "Shift" or "Alt" or "Win";

    /// <param name="fullText">Полные подписи («Control», «Backspace») вместо коротких (тема: <c>text.variant = text</c>).</param>
    /// <param name="mouseGraphic">Кнопки мыши значком (иначе текстом).</param>
    public static KeyCapContent Resolve(KeycapItem item, LabelSet set, LabelMode mode, bool sideMarkers, bool fullText = false, bool mouseGraphic = true)
    {
        string? marker = item.IsUnknown ? "?" : !sideMarkers ? null
            : item.Numpad ? "num"
            : item.Side == KeySide.Right ? "R"
            : null;

        if (Mouse.TryGetValue(item.Key, out var mouse))
        {
            return mouseGraphic
                ? new KeyCapContent(CapLayout.MouseGlyph, mouse.Text, Glyph.Mouse, marker, mouse.Button)
                : new KeyCapContent(CapLayout.TextOnly, mouse.Text, Glyph.None, marker, mouse.Button);
        }

        if (Arrows.TryGetValue(item.Key, out var arrow))
        {
            return mode == LabelMode.Text
                ? new KeyCapContent(CapLayout.TextOnly, arrow.Text, Glyph.None, marker)
                : new KeyCapContent(CapLayout.GlyphOnly, arrow.Text, arrow.Glyph, marker);
        }

        var table = set switch { LabelSet.MacOS => MacOS, LabelSet.Linux => Linux, _ => Windows };
        if (table.TryGetValue(item.Key, out var e) || Media.TryGetValue(item.Key, out e))
        {
            string text = fullText ? e.Full : e.Short;
            bool hasGlyph = e.Glyph != Glyph.None;
            return mode switch
            {
                LabelMode.Text => new KeyCapContent(CapLayout.TextOnly, fullText ? e.Full : e.Text ?? e.Short, Glyph.None, marker),
                LabelMode.Symbol => hasGlyph
                    ? new KeyCapContent(CapLayout.GlyphOnly, e.Text ?? text, e.Glyph, marker)
                    : new KeyCapContent(CapLayout.TextOnly, text, Glyph.None, marker),
                _ => item.Key == "Space" && hasGlyph
                    ? new KeyCapContent(CapLayout.GlyphOnly, text, e.Glyph, marker)  // компактный символ пробела
                    : hasGlyph
                        ? new KeyCapContent(CapLayout.GlyphAndText, text, e.Glyph, marker)
                        : new KeyCapContent(CapLayout.TextOnly, text, Glyph.None, marker),
            };
        }

        // Буквы, цифры, знаки, F-клавиши и неизвестные коды — как есть.
        return item.Key.Length <= 3
            ? new KeyCapContent(CapLayout.Character, item.Key, Glyph.None, marker)
            : new KeyCapContent(CapLayout.TextOnly, item.Key, Glyph.None, marker);
    }
}
