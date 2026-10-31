using System.Windows.Threading;

namespace KeyOverlay.Visual.Rendering;

/// <summary>
/// Искусственные сценарии для превью и «примера на оверлее». Реальный ввод не используется.
/// Каждый проход получает новые Id, как новые появления в модели.
/// </summary>
public sealed class DemoScenario
{
    private const int Ctrl = 1, Shift = 2, K = 3, A = 4, Backspace = 5, Space = 6, Enter = 7, S = 8, Mouse = 9;

    private readonly (double At, Func<long, KeycapItem[]> Items)[] _steps;

    private DemoScenario(TimeSpan length, (double, Func<long, KeycapItem[]>)[] steps)
    {
        Length = length;
        _steps = steps;
    }

    public TimeSpan Length { get; }

    /// <summary>Сочетание с повтором (бейдж «2»), буква, длинная подпись с заменой, повтор во время исчезновения, кнопка мыши.</summary>
    public static DemoScenario Chords { get; } = new(TimeSpan.FromSeconds(7.5),
    [
        (0.0, b => [Mod(b + 1, Ctrl, "Ctrl", true)]),
        (0.3, b => [Mod(b + 1, Ctrl, "Ctrl", true), Mod(b + 2, Shift, "Shift", true)]),
        (0.6, b => [Mod(b + 1, Ctrl, "Ctrl", true), Mod(b + 2, Shift, "Shift", true), Key(b + 3, K, "K", true)]),
        (0.85, b => [Mod(b + 1, Ctrl, "Ctrl", true), Mod(b + 2, Shift, "Shift", true), Key(b + 3, K, "K", false)]),
        (1.05, b => [Mod(b + 1, Ctrl, "Ctrl", true), Mod(b + 2, Shift, "Shift", true), Key(b + 3, K, "K", true, 2)]),
        (1.25, b => [Mod(b + 1, Ctrl, "Ctrl", true), Mod(b + 2, Shift, "Shift", true), Key(b + 3, K, "K", false, 2)]),
        (1.45, b => [Mod(b + 1, Ctrl, "Ctrl", false), Mod(b + 2, Shift, "Shift", false), Key(b + 3, K, "K", false, 2)]),
        (2.6, b => []),
        (3.0, b => [Key(b + 4, A, "A", true)]),
        (3.15, b => [Key(b + 4, A, "A", false)]),
        (3.6, b => [Key(b + 5, Backspace, "Backspace", true)]),
        (3.8, b => [Key(b + 5, Backspace, "Backspace", false)]),
        (4.6, b => []),
        (4.72, b => [Key(b + 6, Backspace, "Backspace", true)]),  // повтор во время исчезновения → тот же кейкап оживает
        (4.9, b => [Key(b + 6, Backspace, "Backspace", false)]),
        (5.6, b => [Mod(b + 7, Ctrl, "Ctrl", true)]),
        (5.8, b => [Mod(b + 7, Ctrl, "Ctrl", true), Key(b + 8, Mouse, "MouseLeft", true)]),
        (6.0, b => [Mod(b + 7, Ctrl, "Ctrl", false), Key(b + 8, Mouse, "MouseLeft", false)]),
        (6.9, b => []),
    ]);

    /// <summary>Быстрая печать «Hello World» (каждая буква заменяет предыдущую), затем Ctrl+S и Enter.</summary>
    public static DemoScenario Typing { get; } = new(TimeSpan.FromSeconds(6.5), BuildTyping());

    /// <summary>Совместимость с 2B: сценарий по умолчанию.</summary>
    public static (long StepKey, IReadOnlyList<KeycapItem> Items) At(TimeSpan t) => Chords.ItemsAt(t);

    public (long StepKey, IReadOnlyList<KeycapItem> Items) ItemsAt(TimeSpan t)
    {
        long loop = t.Ticks / Length.Ticks;
        double inLoop = (t - TimeSpan.FromTicks(Length.Ticks * loop)).TotalSeconds;
        int index = 0;
        for (int i = 0; i < _steps.Length; i++)
        {
            if (_steps[i].At <= inLoop)
            {
                index = i;
            }
        }

        return ((loop * _steps.Length) + index, _steps[index].Items(loop * 1000));
    }

    private static (double, Func<long, KeycapItem[]>)[] BuildTyping()
    {
        var steps = new List<(double, Func<long, KeycapItem[]>)>();
        const string text = "Hello World";
        double t = 0;
        for (int i = 0; i < text.Length; i++)
        {
            int n = i;
            string key = text[i] == ' ' ? "Space" : text[i].ToString().ToUpperInvariant();
            int match = 100 + char.ToUpperInvariant(text[i]);
            // Нажатие и отпускание с коротким перекрытием следующей буквы — как при быстрой печати.
            steps.Add((t, b => [Key(b + 10 + n, match, key, true)]));
            steps.Add((t + 0.09, b => [Key(b + 10 + n, match, key, false)]));
            t += 0.16;
        }

        steps.Add((t + 1.0, b => []));
        steps.Add((t + 1.5, b => [Mod(b + 40, Ctrl, "Ctrl", true)]));
        steps.Add((t + 1.7, b => [Mod(b + 40, Ctrl, "Ctrl", true), Key(b + 41, S, "S", true)]));
        steps.Add((t + 1.85, b => [Mod(b + 40, Ctrl, "Ctrl", false), Key(b + 41, S, "S", false)]));
        steps.Add((t + 2.6, b => [Key(b + 42, Enter, "Enter", true)]));
        steps.Add((t + 2.75, b => [Key(b + 42, Enter, "Enter", false)]));
        steps.Add((t + 3.6, b => []));
        return steps.ToArray();
    }

    private static KeycapItem Mod(long id, int key, string name, bool pressed) => new(id, key, name, true, pressed);

    private static KeycapItem Key(long id, int key, string name, bool pressed, int count = 1) => new(id, key, name, false, pressed, PressCount: count);
}

/// <summary>Проигрывает сценарий в презентер. Таймер останавливается в <see cref="Dispose"/>.</summary>
public sealed class DemoPlayer : IDisposable
{
    private readonly OverlayPresenter _presenter;
    private readonly IAnimationClock _clock;
    private readonly DemoScenario _scenario;
    private readonly Func<IReadOnlyList<KeycapItem>, IReadOnlyList<KeycapItem>> _filter;
    private readonly TimeSpan _start;
    private readonly TimeSpan? _stopAfter;
    private readonly DispatcherTimer _timer;
    private long _lastStep = -1;

    public DemoPlayer(OverlayPresenter presenter, IAnimationClock clock, TimeSpan? stopAfter = null, DemoScenario? scenario = null,
        Func<IReadOnlyList<KeycapItem>, IReadOnlyList<KeycapItem>>? filter = null)
    {
        _presenter = presenter;
        _clock = clock;
        _scenario = scenario ?? DemoScenario.Chords;
        _filter = filter ?? (x => x);
        _start = clock.Now;
        _stopAfter = stopAfter;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Normal, (_, _) => Step(), Dispatcher.CurrentDispatcher);
        Step();
    }

    public bool IsRunning => _timer.IsEnabled;

    public DemoScenario Scenario => _scenario;

    public event Action? Finished;

    /// <summary>Применить фильтр заново (после смены настроек).</summary>
    public void Refresh()
    {
        _lastStep = -1;
        Step();
    }

    public void Dispose() => _timer.Stop();

    private void Step()
    {
        var elapsed = _clock.Now - _start;
        if (_stopAfter is { } stop && elapsed >= stop)
        {
            _timer.Stop();
            _presenter.Update([]);
            Finished?.Invoke();
            return;
        }

        var (key, items) = _scenario.ItemsAt(elapsed);
        if (key != _lastStep)
        {
            _lastStep = key;
            _presenter.Update(_filter(items));
        }
    }
}
