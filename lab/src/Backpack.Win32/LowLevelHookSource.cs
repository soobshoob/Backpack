using System.Runtime.InteropServices;
using Backpack.Core.Diagnostics;
using Backpack.Core.Input;
using Backpack.Core.Transport;
using Backpack.Win32.Interop;

namespace Backpack.Win32;

/// <summary>
/// WH_KEYBOARD_LL + WH_MOUSE_LL в собственном потоке. Callback — статический метод [UnmanagedCallersOnly]:
/// делегата, который мог бы собрать GC, нет. В callback — только разбор структуры, запись в таблицу и
/// кольцо (O(1), без блокировок) и неблокирующий сигнал; затем CallNextHookEx.
/// Проверки рабочего стола и уровня целостности здесь не выполняются.
/// </summary>
public sealed unsafe class LowLevelHookSource : MessageThreadHost
{
    private const uint VkPacket = 0xE7;
    private const uint VkPause = 0x13;
    private const uint VkNumLock = 0x90;
    private const uint MapvkVkToVscEx = 4;

    private static LowLevelHookSource? s_active;

    private nint _keyboardHook;
    private nint _mouseHook;
    private long _callbackErrors;

    public LowLevelHookSource(CaptureState capture)
        : base("Backpack.LowLevelHook") => Capture = capture;

    public CaptureState Capture { get; }

    /// <summary>Длительность callback: вход → перед CallNextHookEx.</summary>
    public LatencyHistogram CallbackTime { get; } = new();

    public long CallbackErrors => Interlocked.Read(ref _callbackErrors);

    public long PacketEvents => Interlocked.Read(ref _packetEvents);

    private long _packetEvents;

    protected override int OnThreadStart(nint hwnd, out string stage)
    {
        if (Interlocked.CompareExchange(ref s_active, this, null) is not null)
        {
            stage = "single instance";
            return 1; // в процессе допускается один LL-источник
        }

        nint module = Native.GetModuleHandle(null);
        stage = "SetWindowsHookEx(WH_KEYBOARD_LL)";
        _keyboardHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, &KeyboardProc, module, 0);
        if (_keyboardHook == 0)
        {
            int e = Marshal.GetLastPInvokeError();
            s_active = null;
            return e == 0 ? -1 : e;
        }

        stage = "SetWindowsHookEx(WH_MOUSE_LL)";
        _mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, &MouseProc, module, 0);
        if (_mouseHook == 0)
        {
            int e = Marshal.GetLastPInvokeError();
            Native.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = 0;
            s_active = null;
            return e == 0 ? -1 : e;
        }

        stage = "running";
        return 0;
    }

    protected override void OnThreadStop()
    {
        if (_keyboardHook != 0 && !Native.UnhookWindowsHookEx(_keyboardHook))
        {
            StopError = Marshal.GetLastPInvokeError();
        }

        if (_mouseHook != 0 && !Native.UnhookWindowsHookEx(_mouseHook))
        {
            StopError = Marshal.GetLastPInvokeError();
        }

        _keyboardHook = 0;
        _mouseHook = 0;
        Interlocked.CompareExchange(ref s_active, null, this);
    }

    protected override void OnSnapshotRequested() => Capture.CreateAndPublishSnapshot();

    [UnmanagedCallersOnly]
    private static nint KeyboardProc(int code, nint wParam, nint lParam)
    {
        var self = s_active;
        if (code >= 0 && self is not null)
        {
            long t0 = Now();
            try
            {
                self.OnKeyboard((KBDLLHOOKSTRUCT*)lParam, t0);
            }
            catch
            {
                Interlocked.Increment(ref self._callbackErrors);
            }

            self.CallbackTime.RecordTicks(Now() - t0);
        }

        return Native.CallNextHookEx(0, code, wParam, lParam);
    }

    [UnmanagedCallersOnly]
    private static nint MouseProc(int code, nint wParam, nint lParam)
    {
        var self = s_active;
        if (code >= 0 && self is not null)
        {
            long t0 = Now();
            try
            {
                self.OnMouse((uint)wParam, (MSLLHOOKSTRUCT*)lParam, t0);
            }
            catch
            {
                Interlocked.Increment(ref self._callbackErrors);
            }

            self.CallbackTime.RecordTicks(Now() - t0);
        }

        return Native.CallNextHookEx(0, code, wParam, lParam);
    }

    private void OnKeyboard(KBDLLHOOKSTRUCT* k, long timestamp)
    {
        uint vk = k->vkCode;
        if (vk == VkPacket)
        {
            Interlocked.Increment(ref _packetEvents); // символ Unicode, не физическая клавиша
            Capture.CountIgnored();
            return;
        }

        uint scan = k->scanCode;
        ScanPrefix prefix;
        if (vk == VkPause)
        {
            prefix = ScanPrefix.E1;
            scan = 0x1D; // нормализация к коду Raw Input (E1 1D); проверяется в X2
        }
        else if (vk == VkNumLock)
        {
            prefix = ScanPrefix.None;
            scan = 0x45; // LL помечает NumLock как extended; Raw Input — без префикса; проверяется в X2
        }
        else if (scan > 0xFF)
        {
            prefix = ScanPrefix.Synthetic; // например, LCtrl от AltGr со scanCode 0x21D
        }
        else
        {
            prefix = (k->flags & 0x01) != 0 ? ScanPrefix.E0 : ScanPrefix.None;
        }

        if ((scan & 0x7F) == 0 && vk is > 0 and < 0xFF)
        {
            // Инжекция только по VK (утилиты ноутбуков так присылают, например, Win+Shift+S от Fn+F6): scan code восстанавливается
            // из VK, как в Raw Input для пустого MakeCode. Событие остаётся помеченным как injected.
            uint ex = Native.MapVirtualKey(vk, MapvkVkToVscEx);
            scan = ex & 0x7F;
            prefix = (ex >> 8) switch { 0xE0 => ScanPrefix.E0, 0xE1 => ScanPrefix.E1, _ => ScanPrefix.None };
        }

        if ((scan & 0x7F) == 0)
        {
            Capture.CountIgnored(); // нет ни scan code, ни соответствия VK — физическую клавишу не определить
            return;
        }

        var flags = InputFlags.None;
        if ((k->flags & 0x10) != 0)
        {
            flags |= InputFlags.Injected;
        }

        if ((k->flags & 0x02) != 0)
        {
            flags |= InputFlags.InjectedLowerIntegrity;
        }

        bool up = (k->flags & 0x80) != 0;
        int slot = new PhysicalKey(prefix, (byte)(scan & 0x7F)).Slot;
        Capture.Observe(up ? InputKind.Up : InputKind.Down, slot, 0, (ushort)vk, flags, timestamp);
    }

    private void OnMouse(uint message, MSLLHOOKSTRUCT* m, long timestamp)
    {
        var flags = InputFlags.None;
        if ((m->flags & 0x01) != 0)
        {
            flags |= InputFlags.Injected;
        }

        if ((m->flags & 0x02) != 0)
        {
            flags |= InputFlags.InjectedLowerIntegrity;
        }

        switch (message)
        {
            case 0x0200: Capture.CountMouseMove(); break;
            case 0x0201: Button(InputKind.Down, MouseButton.Left); break;
            case 0x0202: Button(InputKind.Up, MouseButton.Left); break;
            case 0x0204: Button(InputKind.Down, MouseButton.Right); break;
            case 0x0205: Button(InputKind.Up, MouseButton.Right); break;
            case 0x0207: Button(InputKind.Down, MouseButton.Middle); break;
            case 0x0208: Button(InputKind.Up, MouseButton.Middle); break;
            case 0x020B: Button(InputKind.Down, XButton()); break;
            case 0x020C: Button(InputKind.Up, XButton()); break;
            case 0x020A: // WM_MOUSEWHEEL: + — от себя
                Capture.ObserveWheel(WheelAxis.Vertical, (short)(m->mouseData >> 16), 0, flags, timestamp);
                break;
            case 0x020E: // WM_MOUSEHWHEEL: + — вправо
                Capture.ObserveWheel(WheelAxis.Horizontal, (short)(m->mouseData >> 16), 0, flags, timestamp);
                break;
        }

        MouseButton XButton() => (m->mouseData >> 16) == 2 ? MouseButton.X2 : MouseButton.X1;

        void Button(InputKind kind, MouseButton b) => Capture.Observe(kind, Slots.Mouse(b), 0, 0, flags, timestamp);
    }
}
