using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Backpack.Visual.Layout;

/// <summary>Текст индикатора: код языка крупно и необязательная вторая строка.</summary>
public readonly record struct LayoutLabel(string Code, string? Detail);

/// <summary>
/// HKL → «RU», «EN · Английская (США)», «EN · 2». Младшее слово HKL — LANGID (документировано),
/// старшее — «device handle to the physical layout». Название раскладки для HKL чужого потока документированной функцией
/// не получить (GetKeyboardLayoutName — только свой поток), поэтому используется сопоставление со старым разделом реестра
/// Keyboard Layouts — оно <b>не является документированным контрактом</b>; при неудаче — номер раскладки.
/// </summary>
public static partial class LayoutNames
{
    private static readonly HashSet<int> TransientLangIds = [0x2000, 0x2400, 0x2800, 0x2C00];

    public static int LangId(nint hkl) => (int)((long)hkl & 0xFFFF);

    public static int DeviceWord(nint hkl) => (int)(((long)hkl >> 16) & 0xFFFF);

    public static string? LanguageCode(int langId)
    {
        if (langId == 0 || TransientLangIds.Contains(langId))
        {
            return null;
        }

        try
        {
            var c = CultureInfo.GetCultureInfo(langId);
            string iso = c.TwoLetterISOLanguageName;
            return string.IsNullOrEmpty(iso) || iso == "iv" ? null : iso.ToUpperInvariant();
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Чистая сборка подписи (тестируется): вторая строка — только если у языка несколько раскладок.</summary>
    public static LayoutLabel Compose(string? code, int sameLanguageCount, int indexInLanguage, string? layoutName)
    {
        if (code is null)
        {
            return layoutName is null ? new LayoutLabel("?", null) : new LayoutLabel("⌨", layoutName);
        }

        if (sameLanguageCount <= 1)
        {
            return new LayoutLabel(code, null);
        }

        return new LayoutLabel(code, layoutName ?? (indexInLanguage > 0 ? indexInLanguage.ToString(CultureInfo.InvariantCulture) : null));
    }

    public static LayoutLabel Describe(nint hkl, IReadOnlyList<nint> installed)
    {
        int lang = LangId(hkl);
        var same = installed.Where(h => LangId(h) == lang).ToList();
        int index = same.IndexOf(hkl) + 1;
        string? code = LanguageCode(lang);
        string? name = same.Count > 1 || code is null ? TryLayoutName(hkl) : null;
        return Compose(code, Math.Max(same.Count, 1), index, name);
    }

    /// <summary>Не документированное как контракт сопоставление HKL → KLID → «Layout Display Name» / «Layout Text».</summary>
    public static string? TryLayoutName(nint hkl)
    {
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Keyboard Layouts");
            if (root is null)
            {
                return null;
            }

            int device = DeviceWord(hkl);
            string? klid = null;
            if ((device & 0xF000) == 0xF000)
            {
                int layoutId = device & 0x0FFF;
                foreach (var name in root.GetSubKeyNames())
                {
                    using var k = root.OpenSubKey(name);
                    if (k?.GetValue("Layout Id") is string id && int.TryParse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v) && v == layoutId)
                    {
                        klid = name;
                        break;
                    }
                }
            }
            else if (device != 0)
            {
                klid = device.ToString("X8", CultureInfo.InvariantCulture);
            }

            if (klid is null)
            {
                return null;
            }

            using var key = root.OpenSubKey(klid);
            if (key?.GetValue("Layout Display Name") is string indirect && Indirect(indirect) is { } display)
            {
                return display;
            }

            return key?.GetValue("Layout Text") as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static unsafe string? Indirect(string source)
    {
        char* buffer = stackalloc char[256];
        return SHLoadIndirectString(source, buffer, 256, 0) == 0 ? new string(buffer) : null;
    }

    [LibraryImport("shlwapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static unsafe partial int SHLoadIndirectString(string pszSource, char* pszOutBuf, uint cchOutBuf, nint ppvReserved);
}
