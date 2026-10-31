using System.Globalization;
using System.Runtime.InteropServices;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Layout;

/// <summary>Установленная раскладка Windows: идентификатор (HKL, 8 шестнадцатеричных цифр), название и признак английской.</summary>
public sealed record InstalledLayout(string Id, nint Hkl, string Name, bool IsEnglish);

/// <summary>Список раскладок, установленных в Windows (GetKeyboardLayoutList), с понятными названиями.</summary>
public static class KeyboardLayouts
{
    public static string IdOf(nint hkl) => ((ulong)hkl & 0xFFFFFFFF).ToString("X8", CultureInfo.InvariantCulture);

    /// <summary>
    /// HKL по сохранённому идентификатору (<see cref="IdOf"/>, младшие 32 бита). HKL — знаковое значение: у вариантов раскладок
    /// (например, «Українська (розширена)», F0A80422) старший бит установлен, и на 64-битной Windows HKL равен
    /// FFFFFFFF_F0A80422 — без расширения знака такая раскладка не находилась и подпись пропадала (Phase 4H).
    /// </summary>
    public static nint? Parse(string? id) =>
        SecondaryLabelSettings.IsValidLayoutId(id) && uint.TryParse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v) ? (nint)unchecked((int)v) : null;

    public static IReadOnlyList<InstalledLayout> Installed()
    {
        var list = new List<InstalledLayout>();
        foreach (var hkl in new Win32LayoutProbe().InstalledLayouts())
        {
            int lang = LayoutNames.LangId(hkl);
            string name;
            try
            {
                name = CultureInfo.GetCultureInfo(lang).NativeName;
                name = name.Length > 0 ? char.ToUpper(name[0], CultureInfo.CurrentCulture) + name[1..] : name;
            }
            catch (CultureNotFoundException)
            {
                name = IdOf(hkl);
            }

            if (LayoutNames.DeviceWord(hkl) != lang)
            {
                name += " · " + (LayoutNames.DeviceWord(hkl).ToString("X4", CultureInfo.InvariantCulture));
            }

            list.Add(new InstalledLayout(IdOf(hkl), hkl, name, (lang & 0x3FF) == 0x09));
        }

        return list;
    }
}

/// <summary>
/// Дополнительные подписи раскладки на кейкапах (settings v6). Основная крупная подпись остаётся английской; в левом верхнем
/// углу — символ той же физической клавиши в дополнительной раскладке: активной раскладке целевого приложения (окна переднего
/// плана, а не Backpack) или выбранной установленной. Символ берётся у самой Windows (MapVirtualKeyEx + ToUnicodeEx с флагом
/// «не менять состояние клавиатуры», Windows 10 1607+), поэтому подходит любая установленная раскладка, а не таблица двух языков.
/// <list type="bullet">
/// <item>показывается «легенда» клавиши без модификаторов (как напечатано на клавиатуре): буквы — заглавные, Shift, Caps Lock и
///       AltGr подпись не меняют;</item>
/// <item>мёртвая клавиша — её знак (´, ¨…), состояние ввода не меняется;</item>
/// <item>совпадает с основной подписью (английская раскладка, цифры) — не показывается; служебные клавиши и цифровой блок — нет;</item>
/// <item>IME и раскладки без однозначного символа — нет подписи (или до двух символов, если Windows вернула несколько).</item>
/// </list>
/// Подписи строятся из записей модели отображения, то есть уже после фильтра приватности.
/// </summary>
public static partial class SecondaryLabels
{
    /// <summary>Раскладка целевого приложения (окна переднего плана); задаёт приложение.</summary>
    public static Func<nint>? ActiveLayout { get; set; }

    public static SecondaryLabelSettings Settings { get; set; } = SecondaryLabelSettings.Default;

    private static readonly Dictionary<(int Scan, nint Hkl), string?> Cache = new();

    /// <summary>Scan code набора 1 для символьных клавиш по английской подписи (семантическому имени клавиши).</summary>
    private static readonly Dictionary<string, int> Scans = Build();

    private static Dictionary<string, int> Build()
    {
        var d = new Dictionary<string, int>(StringComparer.Ordinal);
        void Row(string keys, int first)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                d[keys[i].ToString()] = first + i;
            }
        }

        Row("1234567890-=", 0x02);
        Row("QWERTYUIOP[]", 0x10);
        Row("ASDFGHJKL;'`", 0x1E);
        Row("\\ZXCVBNM,./", 0x2B);
        return d;
    }

    /// <summary>Дополнительная подпись для клавиши (null — не показывать).</summary>
    public static string? For(string key, bool numpad)
    {
        var s = Settings;
        if (s.Mode == SecondaryLabelMode.Off || numpad || !Scans.TryGetValue(key, out int scan))
        {
            return null;
        }

        nint hkl = s.Mode == SecondaryLabelMode.FixedLayout ? KeyboardLayouts.Parse(s.Layout) ?? 0 : ActiveLayout?.Invoke() ?? 0;
        if (hkl == 0)
        {
            return null;
        }

        string? label = CharFor(scan, hkl);
        return label is null || string.Equals(label, key, StringComparison.OrdinalIgnoreCase) ? null : label;
    }

    /// <summary>Символ физической клавиши (scan code набора 1) в раскладке <paramref name="hkl"/>; результат кэшируется.</summary>
    public static string? CharFor(int scan, nint hkl)
    {
        if (Cache.TryGetValue((scan, hkl), out var cached))
        {
            return cached;
        }

        string? result = null;
        try
        {
            uint vk = MapVirtualKeyExW((uint)scan, 3 /* MAPVK_VSC_TO_VK_EX */, hkl);
            if (vk != 0)
            {
                var state = new byte[256];
                var buffer = new char[8];
                int n = ToUnicodeEx(vk, (uint)scan, state, buffer, buffer.Length, 0x4 /* не менять состояние клавиатуры */, hkl);
                int count = n < 0 ? 1 : Math.Min(n, 2);
                if (count > 0)
                {
                    string text = new string(buffer, 0, count);
                    if (!text.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
                    {
                        result = text.ToUpper(CultureFor(hkl));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            result = null;
        }

        if (Cache.Count > 4096)
        {
            Cache.Clear();
        }

        Cache[(scan, hkl)] = result;
        return result;
    }

    private static CultureInfo CultureFor(nint hkl)
    {
        try
        {
            return CultureInfo.GetCultureInfo(LayoutNames.LangId(hkl));
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "MapVirtualKeyExW")]
    private static partial uint MapVirtualKeyExW(uint code, uint mapType, nint hkl);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(uint vk, uint scan, byte[] keyState, [Out] char[] buffer, int bufferSize, uint flags, nint hkl);
}
