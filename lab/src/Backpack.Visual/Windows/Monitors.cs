using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Backpack.Visual.Presentation;

namespace Backpack.Visual.Windows;

/// <summary>
/// Монитор в момент перечисления. <see cref="Id"/> — устойчивый идентификатор (путь интерфейса устройства монитора из
/// EnumDisplayDevices с EDD_GET_DEVICE_INTERFACE_NAME: одинаков после перезагрузки и смены порядка, пока монитор подключён
/// к тому же разъёму); если его нет — имя адаптера вывода (<c>\\.\DISPLAYn</c>), которое может меняться.
/// Координаты — физические пиксели виртуального экрана (могут быть отрицательными), <see cref="Scale"/> — DPI / 96.
/// </summary>
public sealed record MonitorDesc(string Id, string Name, PixelRect Bounds, PixelRect Work, double Scale, bool IsPrimary, nint Handle = 0)
{
    /// <summary>Рабочая область в DIP этого монитора, начало — левый верхний угол рабочей области (0, 0).</summary>
    public Rect LocalWorkDip => new(0, 0, Work.Width / Scale, Work.Height / Scale);

    /// <summary>Прямоугольник в DIP рабочей области этого монитора → физические пиксели виртуального экрана.</summary>
    public PixelRect ToPixels(Rect localDip) => new(
        Work.Left + (int)Math.Round(localDip.X * Scale),
        Work.Top + (int)Math.Round(localDip.Y * Scale),
        Math.Max(1, (int)Math.Round(localDip.Width * Scale)),
        Math.Max(1, (int)Math.Round(localDip.Height * Scale)));

    public bool Contains(int x, int y) => x >= Bounds.Left && x < Bounds.Right && y >= Bounds.Top && y < Bounds.Bottom;

    /// <summary>Подпись для списка: имя, разрешение, масштаб, «основной».</summary>
    public string Describe(string primaryWord) =>
        $"{Name} — {Bounds.Width}×{Bounds.Height}, {Scale * 100:0}%{(IsPrimary ? ", " + primaryWord : "")}";
}

/// <summary>Где показывать: основной, выбранный, несколько выбранных одновременно, монитор под курсором (динамически).</summary>
public enum MonitorMode { Primary, Selected, Multiple, UnderCursor }

/// <summary>Сохранённая ссылка на монитор: устойчивый идентификатор и имя для понятного сообщения, если монитора нет.</summary>
public sealed record MonitorRef(string Id, string Name);

/// <summary>Выбор мониторов. Сравнение — по значениям (профиль «изменён / сохранён»).</summary>
public sealed record MonitorTarget(MonitorMode Mode, IReadOnlyList<MonitorRef> Monitors)
{
    public static MonitorTarget Primary { get; } = new(MonitorMode.Primary, []);

    public bool Equals(MonitorTarget? other) => other is not null && Mode == other.Mode && Monitors.SequenceEqual(other.Monitors);

    public override int GetHashCode() => HashCode.Combine(Mode, Monitors.Count);
}

/// <summary>Размещение кейкапов и индикатора раскладки по мониторам (часть профиля).</summary>
/// <param name="IndicatorInherits">Индикатор раскладки показывается на тех же мониторах, что и кейкапы.</param>
public sealed record DisplaySettings(MonitorTarget Keycaps, bool IndicatorInherits, MonitorTarget Indicator)
{
    public static DisplaySettings Default { get; } = new(MonitorTarget.Primary, true, MonitorTarget.Primary);

    public MonitorTarget IndicatorTarget => IndicatorInherits ? Keycaps : Indicator;
}

/// <summary>Результат выбора: мониторы для показа и сохранённые мониторы, которых сейчас нет.</summary>
public sealed record MonitorResolution(IReadOnlyList<MonitorDesc> Monitors, IReadOnlyList<MonitorRef> Missing)
{
    public bool UsedFallback => Missing.Count > 0;
}

/// <summary>
/// Выбор мониторов по настройке — чистая функция (тестируется на вымышленных мониторах с разными DPI и отрицательными
/// координатами). Недоступный сохранённый монитор заменяется основным (если из нескольких выбранных доступен хоть один —
/// показываются доступные). Никогда не возвращает пустой список при наличии хотя бы одного монитора.
/// </summary>
public static class MonitorResolver
{
    public static MonitorResolution Resolve(MonitorTarget target, IReadOnlyList<MonitorDesc> current, (int X, int Y) cursor)
    {
        if (current.Count == 0)
        {
            return new MonitorResolution([], target.Monitors);
        }

        var primary = current.FirstOrDefault(m => m.IsPrimary) ?? current[0];
        switch (target.Mode)
        {
            case MonitorMode.UnderCursor:
                return new MonitorResolution([UnderCursor(current, cursor) ?? primary], []);
            case MonitorMode.Selected:
            case MonitorMode.Multiple:
            {
                var refs = target.Mode == MonitorMode.Selected ? target.Monitors.Take(1) : target.Monitors;
                var found = new List<MonitorDesc>();
                var missing = new List<MonitorRef>();
                foreach (var r in refs)
                {
                    if (current.FirstOrDefault(m => m.Id == r.Id) is { } m)
                    {
                        if (!found.Contains(m))
                        {
                            found.Add(m);
                        }
                    }
                    else
                    {
                        missing.Add(r);
                    }
                }

                if (found.Count == 0)
                {
                    found.Add(primary);
                }

                return new MonitorResolution(found, missing);
            }

            default:
                return new MonitorResolution([primary], []);
        }
    }

    /// <summary>Монитор, содержащий точку; если точка вне всех (переходный момент) — ближайший по расстоянию до границ.</summary>
    public static MonitorDesc? UnderCursor(IReadOnlyList<MonitorDesc> monitors, (int X, int Y) p)
    {
        if (monitors.FirstOrDefault(m => m.Contains(p.X, p.Y)) is { } hit)
        {
            return hit;
        }

        return monitors.OrderBy(m => Distance(m.Bounds, p)).FirstOrDefault();

        static long Distance(PixelRect r, (int X, int Y) p)
        {
            long dx = p.X < r.Left ? r.Left - p.X : p.X >= r.Right ? p.X - r.Right + 1 : 0;
            long dy = p.Y < r.Top ? r.Top - p.Y : p.Y >= r.Bottom ? p.Y - r.Bottom + 1 : 0;
            return (dx * dx) + (dy * dy);
        }
    }
}

/// <summary>Перечисление мониторов (Win32). Без заголовков окон и содержимого экрана: только геометрия и имена устройств.</summary>
public static partial class DisplayMonitors
{
    /// <summary>Подмена перечисления в тестах.</summary>
    public static Func<IReadOnlyList<MonitorDesc>>? Override { get; set; }

    public static unsafe IReadOnlyList<MonitorDesc> Enumerate()
    {
        if (Override is { } o)
        {
            return o();
        }

        var handles = new List<nint>();
        var gc = GCHandle.Alloc(handles);
        try
        {
            EnumDisplayMonitors(0, 0, &Collect, GCHandle.ToIntPtr(gc));
        }
        finally
        {
            gc.Free();
        }

        var result = new List<MonitorDesc>();
        foreach (var h in handles)
        {
            var info = new MonitorInfoEx { Size = sizeof(MonitorInfoEx) };
            if (!GetMonitorInfoW(h, ref info))
            {
                continue;
            }

            string device = new string(info.Device).TrimEnd('\0');
            double scale = GetDpiForMonitor(h, 0, out uint dpi, out _) == 0 && dpi > 0 ? dpi / 96.0 : 1.0;
            var (id, name) = DeviceIdentity(device);
            result.Add(new MonitorDesc(id, name, Rect(info.Monitor), Rect(info.Work), scale, (info.Flags & 1) != 0, h));
        }

        return result;

        static PixelRect Rect(R r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    /// <summary>Устойчивый идентификатор и понятное имя монитора, подключённого к адаптеру вывода <paramref name="device"/>.</summary>
    private static unsafe (string Id, string Name) DeviceIdentity(string device)
    {
        var dd = new DisplayDevice { Size = sizeof(DisplayDevice) };
        if (EnumDisplayDevicesW(device, 0, ref dd, 1 /* EDD_GET_DEVICE_INTERFACE_NAME */))
        {
            string id = new string(dd.DeviceId).TrimEnd('\0');
            string name = new string(dd.DeviceString).TrimEnd('\0');
            if (id.Length > 0)
            {
                return (id, name.Length > 0 ? $"{name} ({device.TrimStart('\\', '.')})" : device);
            }
        }

        return (device, device.TrimStart('\\', '.'));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static int Collect(nint monitor, nint hdc, nint rect, nint data)
    {
        ((List<nint>)GCHandle.FromIntPtr(data).Target!).Add(monitor);
        return 1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct R
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe struct MonitorInfoEx
    {
        public int Size;
        public R Monitor;
        public R Work;
        public uint Flags;
        public fixed char Device[32];
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe struct DisplayDevice
    {
        public int Size;
        public fixed char DeviceName[32];
        public fixed char DeviceString[128];
        public uint StateFlags;
        public fixed char DeviceId[128];
        public fixed char DeviceKey[128];
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool EnumDisplayMonitors(nint hdc, nint clip, delegate* unmanaged[Stdcall]<nint, nint, nint, nint, int> proc, nint data);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(nint monitor, ref MonitorInfoEx info);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumDisplayDevicesW(string device, uint index, ref DisplayDevice dd, uint flags);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);
}

/// <summary>
/// Размещение окна в физических пикселях на нужном мониторе (процесс Per-Monitor V2). WPF при переходе на монитор с другим
/// DPI применяет предложенный системой прямоугольник; после <c>DpiChanged</c> заданный прямоугольник применяется снова.
/// </summary>
public static class PhysicalPlacement
{
    private static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached("PhysicalTarget", typeof(PixelRect?), typeof(PhysicalPlacement));
    private static readonly DependencyProperty HookedProperty = DependencyProperty.RegisterAttached("PhysicalHooked", typeof(bool), typeof(PhysicalPlacement));

    public static PixelRect? TargetOf(Window w) => (PixelRect?)w.GetValue(TargetProperty);

    private static readonly DependencyProperty NotTopmostProperty = DependencyProperty.RegisterAttached("PhysicalNotTopmost", typeof(bool), typeof(PhysicalPlacement));

    public static void Place(Window w, PixelRect r) => Place(w, r, topmost: true);

    /// <summary>То же с выбором слоя: <paramref name="topmost"/> = false — обычное окно (поверх него встают активируемые окна программ).</summary>
    public static void Place(Window w, PixelRect r, bool topmost)
    {
        w.SetValue(NotTopmostProperty, !topmost);
        w.SetValue(TargetProperty, r);
        if (!(bool)w.GetValue(HookedProperty))
        {
            w.SetValue(HookedProperty, true);
            w.DpiChanged += (_, _) => w.Dispatcher.BeginInvoke(() => Apply(w));
        }

        Apply(w);
    }

    private static void Apply(Window w)
    {
        if (TargetOf(w) is not { } r)
        {
            return;
        }

        nint hwnd = new WindowInteropHelper(w).EnsureHandle();
        bool normal = (bool)w.GetValue(NotTopmostProperty);
        SetWindowPos(hwnd, normal ? -2 /* HWND_NOTOPMOST */ : -1 /* HWND_TOPMOST */, r.Left, r.Top, r.Width, r.Height, 0x0010 /* NOACTIVATE */ | 0x0200 /* NOOWNERZORDER */);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
}
