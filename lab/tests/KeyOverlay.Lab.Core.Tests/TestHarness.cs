using System.Diagnostics;
using KeyOverlay.Lab.Core.Input;
using KeyOverlay.Lab.Core.Model;
using KeyOverlay.Lab.Core.Transport;

namespace KeyOverlay.Lab.Core.Tests;

/// <summary>Искусственный источник: тест играет роль и потока захвата, и UI-потока.</summary>
internal sealed class Harness
{
    public const int A = 0x1E, B = 0x30, C = 0x2E, D = 0x20, E = 0x12, F = 0x21, G = 0x22, X = 0x2D;
    public const int LShift = 0x2A, RShift = 0x36, LCtrl = 0x1D;
    public static readonly int RCtrl = new PhysicalKey(ScanPrefix.E0, 0x1D).Slot;
    public static readonly int NumEnter = new PhysicalKey(ScanPrefix.E0, 0x1C).Slot;
    public const int Enter = 0x1C;

    private long _clock = 1_000_000;

    public Harness(int ringCapacity = 16, bool synchronousSnapshot = true, SourceKind kind = SourceKind.RawInput)
    {
        Capture = new CaptureState(kind, ringCapacity, () => Signals++);
        Model = new SourceModel(kind);
        Reader = new SourceReader(Capture, Model, () =>
        {
            SnapshotRequests++;
            if (synchronousSnapshot)
            {
                Capture.CreateAndPublishSnapshot();
            }
        });
    }

    public CaptureState Capture { get; }
    public SourceModel Model { get; }
    public SourceReader Reader { get; }
    public List<KeyTransition> Transitions { get; } = new();
    public int Signals { get; private set; }
    public int SnapshotRequests { get; private set; }

    public long Now => _clock;

    public void Advance(TimeSpan t) => _clock += (long)(t.TotalSeconds * Stopwatch.Frequency);

    public void Down(int slot, int device = 0, ushort vk = 0, InputFlags flags = InputFlags.None) =>
        Capture.Observe(InputKind.Down, slot, device, vk == 0 ? VkFor(slot) : vk, flags, Stopwatch.GetTimestamp());

    public void Up(int slot, int device = 0, ushort vk = 0) =>
        Capture.Observe(InputKind.Up, slot, device, vk == 0 ? VkFor(slot) : vk, InputFlags.None, Stopwatch.GetTimestamp());

    public void Drain(int maxBatch = 1000) => Reader.Drain(maxBatch, Transitions);

    public static ushort VkFor(int slot) => slot switch
    {
        A => 0x41, B => 0x42, C => 0x43, D => 0x44, E => 0x45, F => 0x46, G => 0x47, X => 0x58,
        LShift or RShift => 0x10,
        LCtrl => 0x11,
        Enter => 0x0D,
        _ when slot == RCtrl => 0x11,
        _ when slot == NumEnter => 0x0D,
        _ => 0x7A,
    };
}

internal sealed class FakeProbe : IKeyStateProbe
{
    public ProbeContext Context { get; set; } = new(false, true, true);
    public HashSet<ushort> DownVks { get; } = new();
    public List<ushort> Queried { get; } = new();

    public ProbeContext GetContext() => Context;

    public bool IsDown(ushort vk)
    {
        Queried.Add(vk);
        return DownVks.Contains(vk);
    }
}
