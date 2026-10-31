namespace KeyOverlay.Lab.Core.Diagnostics;

/// <summary>
/// Где остановилась цепочка «захват → модель → приватность → отображение → окно» для последнего показанного нажатия.
/// <see cref="Ok"/> и <see cref="Idle"/> — норма; <see cref="PrivacyClosed"/> и <see cref="IntentionallyHidden"/> — штатные
/// состояния (показ закрыт намеренно); остальные — сбой с указанием этапа.
/// </summary>
public enum DisplayStage : byte
{
    /// <summary>Нажатий не было (или последнее показанное — давно и с тех пор ввода нет).</summary>
    Idle = 0,

    /// <summary>Последнее показанное нажатие дошло до окна, окно видимо и поверх.</summary>
    Ok = 1,

    /// <summary>Система сообщает о вводе, а основной источник событий не получает (захват молчит).</summary>
    NoSourceEvents = 2,

    /// <summary>События приходят, но приватность закрыта (штатно: защищённое поле, запрет, нет данных).</summary>
    PrivacyClosed = 3,

    /// <summary>Показ выключен пользователем (скрыт или выключен инструмент) — штатно.</summary>
    IntentionallyHidden = 4,

    /// <summary>Нажатие прошло фильтр, но модель отображения не была передана окну (обновление вида не выполнено).</summary>
    NotApplied = 5,

    /// <summary>Вид обновлён, но окно оверлея не перестроило кейкапы.</summary>
    NotRefreshed = 6,

    /// <summary>Окно оверлея должно быть видно, но скрыто или свёрнуто.</summary>
    WindowHidden = 7,

    /// <summary>Окно оверлея потеряло флаг topmost.</summary>
    WindowNotTopmost = 8,

    /// <summary>Над группой кейкапов есть другое видимое topmost-окно (наблюдение: окно может быть прозрачным).</summary>
    WindowCovered = 9,
}

/// <summary>Факты об окнах оверлея на момент проверки (собирает UI-поток; здесь только логика).</summary>
/// <param name="SourceSilent">Подтверждённая остановка источника (поток завершился, регистрация Raw Input потеряна).</param>
public readonly record struct DisplayWindowFacts(bool IntendedVisible, bool Visible, bool Topmost, bool Covered, bool PrivacyOpen, bool SourceSilent);

/// <summary>
/// Счётчики конвейера отображения и определение этапа остановки. Без клавиш, текста, заголовков и содержимого окон: только
/// количества, времена и состояния. Время задаёт вызывающий (монотонные часы).
/// </summary>
public sealed class DisplayWatch
{
    /// <summary>Сколько ждать прохождения нажатия до окна, прежде чем считать этап остановившимся.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(1);

    /// <summary>Нажатие старше этого не оценивается (после долгого простоя этап — Idle).</summary>
    public static readonly TimeSpan Horizon = TimeSpan.FromSeconds(30);

    private TimeSpan? _lastKeyEvent;
    private TimeSpan? _lastShownDown;
    private TimeSpan? _lastViewApplied;
    private TimeSpan? _lastRefresh;
    private readonly Dictionary<DisplayStage, long> _entered = new();

    public long KeyEvents { get; private set; }
    public long Shown { get; private set; }
    public long Pending { get; private set; }
    public long Hidden { get; private set; }
    public long ViewsApplied { get; private set; }
    public long Refreshes { get; private set; }
    public int LastVisuals { get; private set; }
    public long Recoveries { get; private set; }

    public DisplayStage Stage { get; private set; } = DisplayStage.Idle;

    /// <summary>Когда начался текущий этап.</summary>
    public TimeSpan StageSince { get; private set; }

    /// <summary>Сколько раз цепочка входила в каждый этап (без Ok/Idle).</summary>
    public IReadOnlyDictionary<DisplayStage, long> Entered => _entered;

    /// <summary>Этап — сбой (не норма и не штатное закрытие показа).</summary>
    public static bool IsFault(DisplayStage s) => s >= DisplayStage.NotApplied;

    public void KeyEvent(TimeSpan now)
    {
        KeyEvents++;
        _lastKeyEvent = now;
    }

    /// <summary>Переход клавиши после фильтра приватности: показан, ждёт решения или скрыт.</summary>
    public void Transition(bool shown, bool pending, bool down, TimeSpan now)
    {
        if (shown)
        {
            Shown++;
            if (down)
            {
                _lastShownDown = now;
            }
        }
        else if (pending)
        {
            Pending++;
        }
        else
        {
            Hidden++;
        }
    }

    public void ViewApplied(TimeSpan now)
    {
        ViewsApplied++;
        _lastViewApplied = now;
    }

    public void Refreshed(int visuals, TimeSpan now)
    {
        Refreshes++;
        LastVisuals = visuals;
        _lastRefresh = now;
    }

    public void Recovered() => Recoveries++;

    /// <summary>Определить этап. Возвращает true, если этап изменился.</summary>
    public bool Evaluate(TimeSpan now, DisplayWindowFacts f)
    {
        var stage = Classify(now, f);
        if (stage == Stage)
        {
            return false;
        }

        Stage = stage;
        StageSince = now;
        if (stage is not (DisplayStage.Ok or DisplayStage.Idle))
        {
            _entered[stage] = _entered.GetValueOrDefault(stage) + 1;
        }

        return true;
    }

    private DisplayStage Classify(TimeSpan now, DisplayWindowFacts f)
    {
        bool recentKey = _lastKeyEvent is { } k && now - k < Horizon;
        if (!f.IntendedVisible)
        {
            return DisplayStage.IntentionallyHidden;
        }

        if (f.SourceSilent)
        {
            return DisplayStage.NoSourceEvents;
        }

        if (_lastShownDown is not { } d || now - d >= Horizon)
        {
            return recentKey && !f.PrivacyOpen ? DisplayStage.PrivacyClosed : DisplayStage.Idle;
        }

        if (recentKey && !f.PrivacyOpen && _lastKeyEvent > d)
        {
            return DisplayStage.PrivacyClosed;
        }

        if (now - d < Grace)
        {
            return Stage; // нажатие ещё в пути
        }

        if (_lastViewApplied is not { } v || v < d)
        {
            return DisplayStage.NotApplied;
        }

        if (_lastRefresh is not { } r || r < v)
        {
            return DisplayStage.NotRefreshed;
        }

        if (!f.Visible)
        {
            return DisplayStage.WindowHidden;
        }

        if (!f.Topmost)
        {
            return DisplayStage.WindowNotTopmost;
        }

        return f.Covered ? DisplayStage.WindowCovered : DisplayStage.Ok;
    }

    /// <summary>Строка журнала: только счётчики и этап (без клавиш).</summary>
    public string Line(TimeSpan now) =>
        $"stage={Stage} since={(now - StageSince).TotalSeconds:F1}s keyEvents={KeyEvents} shown={Shown} pending={Pending} hidden={Hidden} " +
        $"views={ViewsApplied} refreshes={Refreshes} visuals={LastVisuals} recoveries={Recoveries} " +
        $"entered=[{string.Join(",", _entered.OrderBy(e => e.Key).Select(e => $"{e.Key}:{e.Value}"))}]";
}
