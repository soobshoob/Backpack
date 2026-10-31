namespace KeyOverlay.Lab.Core.Input;

public enum SourceKind : byte { RawInput = 0, LowLevelHook = 1 }

public enum InputKind : byte
{
    Down = 0,
    Up = 1,

    /// <summary>Источник наблюдал удаление устройства; все его слоты отпущены. <see cref="InputEvent.Slot"/> не используется.</summary>
    DeviceRemoved = 2,

    /// <summary>
    /// Импульс колеса мыши. <see cref="InputEvent.Slot"/> — ось (<see cref="WheelAxis"/>), <see cref="InputEvent.Delta"/> — знаковое
    /// смещение в единицах WHEEL_DELTA (120 = один щелчок; высокоточные колёса присылают меньшие значения).
    /// Колесо — не удерживаемое состояние: таблица Down/Up и снимки его не содержат.
    /// </summary>
    Wheel = 3,
}

/// <summary>Ось прокрутки: вертикальная (+ — от себя, «вверх») и горизонтальная (+ — вправо).</summary>
public enum WheelAxis : byte { Vertical = 0, Horizontal = 1 }

[Flags]
public enum InputFlags : byte
{
    None = 0,

    /// <summary>Down для слота, который источник уже считал нажатым (автоповтор или повтор после потерянного Up).</summary>
    Repeat = 1,

    Injected = 2,
    InjectedLowerIntegrity = 4,
}

/// <summary>
/// Нормализованное событие источника. Хранится только в памяти; в файлы и экспорт не попадает.
/// </summary>
/// <param name="Seq">Сквозной номер события в пределах источника (1, 2, …).</param>
/// <param name="CaptureTimestamp">Stopwatch.GetTimestamp() в момент получения события потоком захвата.</param>
/// <param name="Slot">Слот устройства: клавиша (0..511) или кнопка мыши (512..516).</param>
/// <param name="Device">Индекс устройства 0..7 (у LL-хука всегда 0).</param>
/// <param name="Vk">Наблюдавшийся VK (может быть общим для левой и правой клавиши).</param>
/// <param name="Delta">Только для <see cref="InputKind.Wheel"/>: знаковое смещение колеса; иначе 0.</param>
public readonly record struct InputEvent(
    long Seq,
    long CaptureTimestamp,
    InputKind Kind,
    ushort Slot,
    byte Device,
    ushort Vk,
    InputFlags Flags,
    short Delta = 0);
