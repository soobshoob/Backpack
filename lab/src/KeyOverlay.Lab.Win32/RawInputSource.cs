using System.Runtime.InteropServices;
using KeyOverlay.Lab.Core.Diagnostics;
using KeyOverlay.Lab.Core.Input;
using KeyOverlay.Lab.Core.Transport;
using KeyOverlay.Lab.Win32.Interop;

namespace KeyOverlay.Lab.Win32;

/// <summary>
/// Raw Input (клавиатура 0x01/0x06 и мышь 0x01/0x02) с RIDEV_INPUTSINK | RIDEV_DEVNOTIFY на message-only окне
/// собственного потока. Устройства различаются по hDevice: индекс 0 — hDevice == 0 (неизвестное устройство),
/// 1..7 — реальные устройства (8-е и далее делят индекс 7).
/// </summary>
public sealed unsafe class RawInputSource : MessageThreadHost
{
    private const ushort KeyboardOverrunMakeCode = 0xFF;
    private const uint MapvkVkToVscEx = 4;

    private readonly Dictionary<nint, int> _devices = new();
    private readonly byte* _buffer = (byte*)NativeMemory.Alloc(256);
    private int _nextDevice = 1;

    public RawInputSource(CaptureState capture)
        : base("KeyOverlay.RawInput") => Capture = capture;

    public CaptureState Capture { get; }

    /// <summary>Длительность обработки одного WM_INPUT в потоке источника.</summary>
    public LatencyHistogram MessageTime { get; } = new();

    public int DevicesSeen => _devices.Count;

    /// <summary>Подтверждаемая проверка: зарегистрировано ли получение клавиатуры и мыши именно нашим окном.</summary>
    public bool? IsRegistrationIntact()
    {
        if (Status != HostStatus.Running)
        {
            return null;
        }

        uint count = 0;
        uint size = (uint)sizeof(RAWINPUTDEVICE);
        Native.GetRegisteredRawInputDevices(null, &count, size);
        if (count == 0)
        {
            return false;
        }

        var list = stackalloc RAWINPUTDEVICE[(int)Math.Min(count, 32)];
        uint n = Math.Min(count, 32);
        if (Native.GetRegisteredRawInputDevices(list, &n, size) == unchecked((uint)-1))
        {
            return null;
        }

        bool kb = false, mouse = false;
        for (int i = 0; i < n; i++)
        {
            if (list[i].usUsagePage == 1 && list[i].hwndTarget == Hwnd)
            {
                kb |= list[i].usUsage == 6;
                mouse |= list[i].usUsage == 2;
            }
        }

        return kb && mouse;
    }

    protected override int OnThreadStart(nint hwnd, out string stage)
    {
        stage = "RegisterRawInputDevices";
        var devices = stackalloc RAWINPUTDEVICE[2];
        devices[0] = new RAWINPUTDEVICE { usUsagePage = 1, usUsage = 6, dwFlags = Native.RIDEV_INPUTSINK | Native.RIDEV_DEVNOTIFY, hwndTarget = hwnd };
        devices[1] = new RAWINPUTDEVICE { usUsagePage = 1, usUsage = 2, dwFlags = Native.RIDEV_INPUTSINK | Native.RIDEV_DEVNOTIFY, hwndTarget = hwnd };
        return Native.RegisterRawInputDevices(devices, 2, (uint)sizeof(RAWINPUTDEVICE)) ? 0 : Marshal.GetLastPInvokeError();
    }

    protected override void OnThreadStop()
    {
        var devices = stackalloc RAWINPUTDEVICE[2];
        devices[0] = new RAWINPUTDEVICE { usUsagePage = 1, usUsage = 6, dwFlags = Native.RIDEV_REMOVE, hwndTarget = 0 };
        devices[1] = new RAWINPUTDEVICE { usUsagePage = 1, usUsage = 2, dwFlags = Native.RIDEV_REMOVE, hwndTarget = 0 };
        if (!Native.RegisterRawInputDevices(devices, 2, (uint)sizeof(RAWINPUTDEVICE)))
        {
            StopError = Marshal.GetLastPInvokeError();
        }

        NativeMemory.Free(_buffer);
    }

    protected override void OnSnapshotRequested() => Capture.CreateAndPublishSnapshot();

    protected override bool OnMessage(nint hwnd, uint msg, nint wParam, nint lParam, out nint result)
    {
        result = 0;
        if (msg == Native.WM_INPUT)
        {
            long t0 = Now();
            ReadInput(lParam, t0);
            MessageTime.RecordTicks(Now() - t0);
            return false; // DefWindowProc обязателен для WM_INPUT (очистка)
        }

        if (msg == Native.WM_INPUT_DEVICE_CHANGE && (int)wParam == Native.GIDC_REMOVAL)
        {
            if (_devices.Remove(lParam, out int index))
            {
                Capture.RemoveDevice(index, Now());
            }

            return true;
        }

        return false;
    }

    private void ReadInput(nint hRawInput, long timestamp)
    {
        uint size = 256;
        uint read = Native.GetRawInputData(hRawInput, Native.RID_INPUT, _buffer, &size, (uint)sizeof(RAWINPUTHEADER));
        if (read == unchecked((uint)-1) || read < sizeof(RAWINPUTHEADER))
        {
            Capture.CountIgnored();
            return;
        }

        var header = (RAWINPUTHEADER*)_buffer;
        byte* data = _buffer + sizeof(RAWINPUTHEADER);
        int device = DeviceIndex(header->hDevice);

        if (header->dwType == Native.RIM_TYPEKEYBOARD)
        {
            ReadKeyboard((RAWKEYBOARD*)data, device, timestamp);
        }
        else if (header->dwType == Native.RIM_TYPEMOUSE)
        {
            ReadMouse((RAWMOUSE*)data, device, timestamp);
        }
    }

    private void ReadKeyboard(RAWKEYBOARD* kb, int device, long timestamp)
    {
        // Как в примере MS к RAWKEYBOARD: пропустить переполнение и события без VK (VKey ≥ 255 — фиктивные).
        if (kb->MakeCode == KeyboardOverrunMakeCode || kb->VKey >= 0xFF)
        {
            Capture.CountIgnored();
            return;
        }

        ScanPrefix prefix = (kb->Flags & 2) != 0 ? ScanPrefix.E0 : (kb->Flags & 4) != 0 ? ScanPrefix.E1 : ScanPrefix.None;
        int scan = kb->MakeCode & 0x7F;
        if (kb->MakeCode == 0)
        {
            // Пустой scan code (например, мультимедиа-клавиши): восстановить из VK, как советует MS.
            uint ex = Native.MapVirtualKey(kb->VKey, MapvkVkToVscEx);
            scan = (int)(ex & 0x7F);
            prefix = (ex >> 8) switch { 0xE0 => ScanPrefix.E0, 0xE1 => ScanPrefix.E1, _ => ScanPrefix.None };
            if (scan == 0)
            {
                Capture.CountIgnored();
                return;
            }
        }

        bool up = (kb->Flags & 1) != 0;
        int slot = new PhysicalKey(prefix, (byte)scan).Slot;
        Capture.Observe(up ? InputKind.Up : InputKind.Down, slot, device, kb->VKey, InputFlags.None, timestamp);
    }

    private void ReadMouse(RAWMOUSE* m, int device, long timestamp)
    {
        ushort f = m->usButtonFlags;
        if (m->lLastX != 0 || m->lLastY != 0)
        {
            Capture.CountMouseMove();
        }

        Button(f, 0x0001, 0x0002, MouseButton.Left);
        Button(f, 0x0004, 0x0008, MouseButton.Right);
        Button(f, 0x0010, 0x0020, MouseButton.Middle);
        Button(f, 0x0040, 0x0080, MouseButton.X1);
        Button(f, 0x0100, 0x0200, MouseButton.X2);

        // RI_MOUSE_WHEEL (0x0400) и RI_MOUSE_HWHEEL (0x0800): знаковое смещение в usButtonData, тот же смысл знака, что у WM_MOUSE(H)WHEEL.
        if ((f & 0x0400) != 0)
        {
            Capture.ObserveWheel(WheelAxis.Vertical, (short)m->usButtonData, device, InputFlags.None, timestamp);
        }

        if ((f & 0x0800) != 0)
        {
            Capture.ObserveWheel(WheelAxis.Horizontal, (short)m->usButtonData, device, InputFlags.None, timestamp);
        }

        void Button(ushort flags, ushort downBit, ushort upBit, MouseButton b)
        {
            if ((flags & downBit) != 0)
            {
                Capture.Observe(InputKind.Down, Slots.Mouse(b), device, 0, InputFlags.None, timestamp);
            }

            if ((flags & upBit) != 0)
            {
                Capture.Observe(InputKind.Up, Slots.Mouse(b), device, 0, InputFlags.None, timestamp);
            }
        }
    }

    private int DeviceIndex(nint hDevice)
    {
        if (hDevice == 0)
        {
            return 0;
        }

        if (_devices.TryGetValue(hDevice, out int index))
        {
            return index;
        }

        index = Math.Min(_nextDevice, Slots.MaxDevices - 1);
        if (_nextDevice < Slots.MaxDevices - 1)
        {
            _nextDevice++;
        }

        _devices[hDevice] = index;
        return index;
    }
}
