namespace Backpack.Core.Input;

/// <summary>
/// Префикс scan code. <see cref="Synthetic"/> — коды вне диапазона 0x00..0xFF, которые система
/// присылает для синтезированных раскладкой событий (например, LCtrl при AltGr в LL-хуке, scanCode 0x21D).
/// </summary>
public enum ScanPrefix : byte { None = 0, E0 = 1, E1 = 2, Synthetic = 3 }

/// <summary>
/// Физическая клавиша: scan code набора 1 (7 бит) + префикс. Не зависит от раскладки и VK.
/// Устройство в идентичность клавиши не входит — оно отдельное измерение таблицы состояния.
/// </summary>
public readonly record struct PhysicalKey(ScanPrefix Prefix, byte Scan)
{
    public static PhysicalKey FromSlot(int keySlot) => new((ScanPrefix)(keySlot >> 7), (byte)(keySlot & 0x7F));

    public int Slot => ((int)Prefix << 7) | (Scan & 0x7F);

    public override string ToString() => Prefix switch
    {
        ScanPrefix.None => $"{Scan:X2}",
        ScanPrefix.E0 => $"E0 {Scan:X2}",
        ScanPrefix.E1 => $"E1 {Scan:X2}",
        _ => $"S {Scan:X2}",
    };
}

public enum MouseButton : byte { Left = 0, Right = 1, Middle = 2, X1 = 3, X2 = 4 }

/// <summary>Слоты таблицы состояния одного устройства: 512 клавиш (4 префикса × 128) + 5 кнопок мыши.</summary>
public static class Slots
{
    public const int KeySlots = 512;
    public const int MouseSlots = 5;
    public const int PerDevice = KeySlots + MouseSlots;
    public const int MaxDevices = 8;
    public const int Total = PerDevice * MaxDevices;

    public static int Mouse(MouseButton b) => KeySlots + (int)b;
    public static bool IsMouse(int slot) => slot >= KeySlots;
    public static int Index(int device, int slot) => device * PerDevice + slot;
}

public enum ModifierKind : byte { None = 0, Ctrl = 1, Shift = 2, Alt = 3, Win = 4 }

/// <summary>Текст кейкапа: <see cref="Main"/> — крупная подпись, <see cref="Hint"/> — мелкая пометка или null.</summary>
public readonly record struct KeyCapText(string Main, string? Hint);

/// <summary>Семантическое имя клавиши, сторона ('L', 'R', ' ') и признак цифрового блока.</summary>
public readonly record struct KeySemantic(string Key, char Side, bool Numpad);

/// <summary>
/// Собственная таблица подписей стенда (scan code набора 1, US-раскладка).
/// Подписи — не данные Keyviz; учёт текущей раскладки сознательно отложен.
/// </summary>
public static class KeyInfo
{
    private static readonly Dictionary<PhysicalKey, string> Labels = Build();

    public static string Label(int slot)
    {
        if (Slots.IsMouse(slot))
        {
            return (MouseButton)(slot - Slots.KeySlots) switch
            {
                MouseButton.Left => "Mouse L",
                MouseButton.Right => "Mouse R",
                MouseButton.Middle => "Mouse M",
                MouseButton.X1 => "Mouse X1",
                _ => "Mouse X2",
            };
        }

        var key = PhysicalKey.FromSlot(slot);
        if (Labels.TryGetValue(key, out var label))
        {
            return label;
        }

        return key.Prefix == ScanPrefix.Synthetic && Labels.TryGetValue(key with { Prefix = ScanPrefix.None }, out var baseLabel)
            ? baseLabel + "*"
            : "SC " + key;
    }

    /// <summary>
    /// Подпись для кейкапа: основная строка и необязательная пометка (сторона модификатора, «Num»).
    /// Строится из собственной таблицы <see cref="Label"/>, чтобы левые/правые и Enter/Num Enter оставались различимы.
    /// </summary>
    public static KeyCapText CapText(int slot)
    {
        string label = Label(slot);
        if (Slots.IsMouse(slot))
        {
            return new KeyCapText(label["Mouse ".Length..], "Mouse");
        }

        return label switch
        {
            "LCtrl" or "LShift" or "LAlt" or "LWin" => new KeyCapText(label[1..], "L"),
            "RCtrl" or "RShift" or "RAlt" or "RWin" => new KeyCapText(label[1..], "R"),
            "Num Enter" => new KeyCapText("Enter", "Num"),
            _ when label.StartsWith("Num ", StringComparison.Ordinal) => new KeyCapText(label["Num ".Length..], "Num"),
            "PgUp" => new KeyCapText("Pg Up", null),
            "PgDn" => new KeyCapText("Pg Dn", null),
            "PrtSc" => new KeyCapText("Prt Sc", null),
            _ => new KeyCapText(label, null),
        };
    }

    /// <summary>
    /// Семантическое имя физической клавиши для визуального слоя: «Ctrl», «Enter», «ArrowUp», «MouseLeft», «A», «7»…
    /// Сторона — 'L', 'R' или ' '; <c>Numpad</c> — клавиша цифрового блока. Подписи и символы выбирает визуальный слой
    /// по набору обозначений (Windows/macOS/Linux), назначение клавиши при этом не меняется.
    /// </summary>
    public static KeySemantic Semantic(int slot)
    {
        if (Slots.IsMouse(slot))
        {
            return new KeySemantic((MouseButton)(slot - Slots.KeySlots) switch
            {
                MouseButton.Left => "MouseLeft",
                MouseButton.Right => "MouseRight",
                MouseButton.Middle => "MouseMiddle",
                MouseButton.X1 => "MouseX1",
                _ => "MouseX2",
            }, ' ', false);
        }

        string label = Label(slot);
        return label switch
        {
            "LCtrl" or "LShift" or "LAlt" or "LWin" => new KeySemantic(label[1..], 'L', false),
            "RCtrl" or "RShift" or "RAlt" or "RWin" => new KeySemantic(label[1..], 'R', false),
            "Shift (E0 fake)" => new KeySemantic("Shift", ' ', false),
            "Num Enter" => new KeySemantic("Enter", ' ', true),
            _ when label.StartsWith("Num ", StringComparison.Ordinal) => new KeySemantic(label["Num ".Length..], ' ', true),
            "Up" => new KeySemantic("ArrowUp", ' ', false),
            "Down" => new KeySemantic("ArrowDown", ' ', false),
            "Left" => new KeySemantic("ArrowLeft", ' ', false),
            "Right" => new KeySemantic("ArrowRight", ' ', false),
            "PgUp" => new KeySemantic("PageUp", ' ', false),
            "PgDn" => new KeySemantic("PageDown", ' ', false),
            "PrtSc" => new KeySemantic("PrintScreen", ' ', false),
            "IntlBackslash" => new KeySemantic("\\", ' ', false),
            _ => new KeySemantic(label, ' ', false), // Esc, Tab, Enter, Backspace, Space, Delete, F1…, буквы, цифры, знаки, «SC xx»
        };

    }

    public static ModifierKind Modifier(int slot)
    {
        if (Slots.IsMouse(slot))
        {
            return ModifierKind.None;
        }

        var key = PhysicalKey.FromSlot(slot);
        return (key.Prefix, key.Scan) switch
        {
            (ScanPrefix.None, 0x1D) or (ScanPrefix.E0, 0x1D) or (ScanPrefix.Synthetic, 0x1D) => ModifierKind.Ctrl,
            (ScanPrefix.None, 0x2A) or (ScanPrefix.None, 0x36) => ModifierKind.Shift,
            (ScanPrefix.None, 0x38) or (ScanPrefix.E0, 0x38) => ModifierKind.Alt,
            (ScanPrefix.E0, 0x5B) or (ScanPrefix.E0, 0x5C) => ModifierKind.Win,
            _ => ModifierKind.None,
        };
    }

    /// <summary>
    /// VK для запроса GetAsyncKeyState. Для модификаторов — лево/право-специфичный код по физической клавише;
    /// для остальных — последний наблюдавшийся VK. 0 — клавишу зондировать нельзя.
    /// </summary>
    public static ushort ProbeVk(int slot, ushort observedVk)
    {
        if (Slots.IsMouse(slot))
        {
            return 0; // GetAsyncKeyState проверяет физические кнопки без учёта перестановки — не зондируем.
        }

        var key = PhysicalKey.FromSlot(slot);
        return (key.Prefix, key.Scan) switch
        {
            (ScanPrefix.None, 0x2A) => 0xA0,
            (ScanPrefix.None, 0x36) => 0xA1,
            (ScanPrefix.None, 0x1D) or (ScanPrefix.Synthetic, 0x1D) => 0xA2,
            (ScanPrefix.E0, 0x1D) => 0xA3,
            (ScanPrefix.None, 0x38) => 0xA4,
            (ScanPrefix.E0, 0x38) => 0xA5,
            (ScanPrefix.E0, 0x5B) => 0x5B,
            (ScanPrefix.E0, 0x5C) => 0x5C,
            _ => observedVk is > 0 and < 0xFF && observedVk is not (0x10 or 0x11 or 0x12) ? observedVk : (ushort)0,
        };
    }

    private static Dictionary<PhysicalKey, string> Build()
    {
        var d = new Dictionary<PhysicalKey, string>();
        void N(byte scan, string label) => d[new PhysicalKey(ScanPrefix.None, scan)] = label;
        void E(byte scan, string label) => d[new PhysicalKey(ScanPrefix.E0, scan)] = label;

        N(0x01, "Esc");
        string digits = "1234567890";
        for (int i = 0; i < digits.Length; i++)
        {
            N((byte)(0x02 + i), digits[i].ToString());
        }

        N(0x0C, "-"); N(0x0D, "="); N(0x0E, "Backspace"); N(0x0F, "Tab");
        string row1 = "QWERTYUIOP";
        for (int i = 0; i < row1.Length; i++)
        {
            N((byte)(0x10 + i), row1[i].ToString());
        }

        N(0x1A, "["); N(0x1B, "]"); N(0x1C, "Enter"); N(0x1D, "LCtrl");
        string row2 = "ASDFGHJKL";
        for (int i = 0; i < row2.Length; i++)
        {
            N((byte)(0x1E + i), row2[i].ToString());
        }

        N(0x27, ";"); N(0x28, "'"); N(0x29, "`"); N(0x2A, "LShift"); N(0x2B, "\\");
        string row3 = "ZXCVBNM";
        for (int i = 0; i < row3.Length; i++)
        {
            N((byte)(0x2C + i), row3[i].ToString());
        }

        N(0x33, ","); N(0x34, "."); N(0x35, "/"); N(0x36, "RShift"); N(0x37, "Num *");
        N(0x38, "LAlt"); N(0x39, "Space"); N(0x3A, "CapsLock");
        for (int i = 0; i < 10; i++)
        {
            N((byte)(0x3B + i), "F" + (i + 1));
        }

        N(0x45, "NumLock"); N(0x46, "ScrollLock");
        N(0x47, "Num 7"); N(0x48, "Num 8"); N(0x49, "Num 9"); N(0x4A, "Num -");
        N(0x4B, "Num 4"); N(0x4C, "Num 5"); N(0x4D, "Num 6"); N(0x4E, "Num +");
        N(0x4F, "Num 1"); N(0x50, "Num 2"); N(0x51, "Num 3"); N(0x52, "Num 0"); N(0x53, "Num .");
        N(0x56, "IntlBackslash"); N(0x57, "F11"); N(0x58, "F12");

        E(0x1C, "Num Enter"); E(0x1D, "RCtrl"); E(0x35, "Num /"); E(0x37, "PrtSc"); E(0x38, "RAlt");
        E(0x47, "Home"); E(0x48, "Up"); E(0x49, "PgUp"); E(0x4B, "Left"); E(0x4D, "Right");
        E(0x4F, "End"); E(0x50, "Down"); E(0x51, "PgDn"); E(0x52, "Insert"); E(0x53, "Delete");
        E(0x5B, "LWin"); E(0x5C, "RWin"); E(0x5D, "Menu");
        E(0x2A, "Shift (E0 fake)"); E(0x36, "Shift (E0 fake)");

        // F13–F24 и мультимедиа (Phase 4G): клавиатуры и ноутбуки присылают их обычными scan code (набор 1, префикс E0).
        // Fn сама до Windows не доходит: прошивка превращает Fn+F1 в E0 20 (Mute) и т. п. — показывается результат, а не «Fn».
        for (int i = 0; i < 11; i++)
        {
            N((byte)(0x64 + i), "F" + (13 + i));
        }

        N(0x76, "F24");
        E(0x20, "VolumeMute"); E(0x2E, "VolumeDown"); E(0x30, "VolumeUp");
        E(0x22, "MediaPlayPause"); E(0x24, "MediaStop"); E(0x19, "MediaNext"); E(0x10, "MediaPrev"); E(0x6D, "MediaSelect");
        E(0x21, "LaunchApp2"); E(0x6B, "LaunchApp1"); E(0x6C, "LaunchMail");
        E(0x32, "BrowserHome"); E(0x65, "BrowserSearch"); E(0x66, "BrowserFavorites"); E(0x67, "BrowserRefresh");
        E(0x68, "BrowserStop"); E(0x69, "BrowserForward"); E(0x6A, "BrowserBack");
        E(0x5E, "Power"); E(0x5F, "Sleep"); E(0x63, "Wake");
        d[new PhysicalKey(ScanPrefix.E1, 0x1D)] = "Pause";
        return d;
    }
}
