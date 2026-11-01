using Backpack.Core.Input;
using Backpack.Core.Model;

namespace Backpack.Core.Privacy;

/// <summary>Состояние динамической приватности. Показ разрешён только в <see cref="Allowed"/>.</summary>
public enum PrivacyState : byte
{
    /// <summary>Проверка нового контекста фокуса: ввод этого контекста ждёт решения (не показывается до него).</summary>
    Checking = 0,

    /// <summary>Обычное поле: сфокусированный элемент сообщил IsPassword = false и приложение не в списке запрета.</summary>
    Allowed = 1,

    /// <summary>Защищённое поле (IsPassword = true) или приложение из списка запрета.</summary>
    Protected = 2,

    /// <summary>Нет достоверных данных: ошибка, таймаут, свойство не поддерживается, элемент недоступен.</summary>
    Unknown = 3,
}

/// <summary>Почему показ закрыт (для статуса; без имён полей, текста и количества скрытых нажатий).</summary>
public enum PrivacyReason : byte
{
    None = 0,
    PasswordField = 1,
    BlockedApp = 2,
    Checking = 3,
    NoData = 4,
    Timeout = 5,
}

/// <summary>Результат одной проверки сфокусированного элемента (только метаданные, без содержимого).</summary>
public enum ProbeVerdict : byte
{
    /// <summary>IsPassword получено и равно false.</summary>
    NotPassword = 0,

    /// <summary>IsPassword получено и равно true.</summary>
    Password = 1,

    /// <summary>Свойство не поддерживается провайдером, элемента нет, ошибка COM/UIA.</summary>
    NoData = 2,

    /// <summary>Приложение переднего плана в списке запрета визуализации.</summary>
    BlockedApp = 3,
}

/// <summary>
/// Фильтр динамической приватности перед моделью отображения (и всем, что получает ввод после неё: оверлей, история,
/// расширения). Чистая логика без потоков и Win32: версии контекста и времена задаёт вызывающий.
/// <para>
/// <b>Контекст по времени нажатия.</b> Каждая смена фокуса начинает новую версию контекста с меткой времени её начала
/// (Stopwatch). Переход клавиши относится к контексту, в котором он был <i>захвачен</i> (<see cref="KeyTransition.At"/>), а не к
/// контексту на момент обработки: Tab или Win+E, нажатые в подтверждённом обычном поле, показываются, даже если вызванная ими
/// смена фокуса пришла раньше, чем порция ввода была применена. Переход без времени (0) относится к текущему контексту.
/// </para>
/// <list type="bullet">
/// <item><see cref="PrivacyState.Allowed"/> — показ как без приватности;</item>
/// <item><see cref="PrivacyState.Checking"/> — нажатия этой версии <b>ждут</b> (не показываются); если именно эта версия
///       подтверждена обычным полем — они передаются по порядку (<see cref="TakeReleased"/>), иначе (защищённое поле, нет данных,
///       таймаут, новая смена фокуса, переполнение) — отбрасываются окончательно и больше никогда не появляются;</item>
/// <item><see cref="PrivacyState.Protected"/> — скрывается весь ввод, включая сочетания; отображение очищается
///       (<see cref="ClearRequested"/>), удерживаемые клавиши скрыты до их отпускания;</item>
/// <item><see cref="PrivacyState.Unknown"/> — новый ввод скрыт (не ждёт); уже показанные клавиши подтверждённого обычного поля
///       остаются и отпускаются как обычно;</item>
/// <item>результат старой версии игнорируется; скрытое не буферизуется и не воспроизводится;</item>
/// <item>кнопки мыши и колесо не фильтруются (это не набираемый текст).</item>
/// </list>
/// Остаточная гонка: если уведомление о смене фокуса доставлено позже нажатия в новом поле, это нажатие относится к прежнему
/// контексту. Узнать момент смены фокуса раньше уведомления нельзя — универсальной защиты нет.
/// </summary>
public sealed class PrivacyGate
{
    /// <summary>Не больше стольких переходов ждут решения проверки; при переполнении все ожидающие отбрасываются.</summary>
    public const int MaxPending = 64;

    private const int HistoryDepth = 32;

    private readonly HashSet<int> _down = new();
    private readonly Queue<string> _history = new();
    private readonly HashSet<int> _hidden = new();
    private readonly HashSet<int> _shown = new();
    private readonly List<KeyTransition> _pending = new();
    private readonly HashSet<int> _pendingDown = new();
    private readonly List<KeyTransition> _released = new();
    private readonly List<(long Version, long Start, PrivacyState Final)> _past = new();
    private long _currentStart;
    private bool _enabled = true;

    public PrivacyGate(TimeSpan probeTimeout)
    {
        ProbeTimeout = probeTimeout;
    }

    /// <summary>Сколько ждать результата проверки, прежде чем считать состояние «нет данных».</summary>
    public TimeSpan ProbeTimeout { get; set; }

    /// <summary>Текущая версия контекста фокуса (растёт при каждой смене).</summary>
    public long Version { get; private set; }

    public PrivacyState State { get; private set; } = PrivacyState.Checking;

    public PrivacyReason Reason { get; private set; } = PrivacyReason.Checking;

    /// <summary>Срок ожидания результата текущей версии (null — не ждём).</summary>
    public TimeSpan? Deadline { get; private set; }

    /// <summary>Показ разрешён: функция выключена или проверка текущей версии дала «обычное поле».</summary>
    public bool Open => !_enabled || State == PrivacyState.Allowed;

    /// <summary>Динамическая приватность включена. Включение очищает отображение: показанное до него могло быть набрано в защищённом поле.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            bool wasOpen = Open;
            _enabled = value;
            if (!value)
            {
                Discard();
            }
            else if (wasOpen && !Open)
            {
                CloseNow();
            }

            Changed?.Invoke();
        }
    }

    /// <summary>Клавиши, скрытые до их отпускания (для тестов).</summary>
    public int HiddenHeldCount => _hidden.Count;

    /// <summary>Переходов ждут решения проверки текущей версии.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>Сколько раз показ закрывался с очисткой (для диагностики, без связи с нажатиями).</summary>
    public long Closures { get; private set; }

    /// <summary>Сколько результатов пришло для устаревшей версии.</summary>
    public long StaleResults { get; private set; }

    /// <summary>Сколько проверок завершились по таймауту.</summary>
    public long Timeouts { get; private set; }

    /// <summary>Сколько раз ожидающие переходы были переданы после подтверждения и сколько раз отброшены (без числа нажатий).</summary>
    public long PendingReleases { get; private set; }

    public long PendingDiscards { get; private set; }

    /// <summary>
    /// Последние переходы для диагностики самопроверки: только номера версий, ответы и состояния (без ввода и без сведений
    /// о полях). Хранятся в памяти, не более 24 записей.
    /// </summary>
    public IReadOnlyCollection<string> History => _history;

    private void Note(string s)
    {
        _history.Enqueue(s);
        while (_history.Count > 24)
        {
            _history.Dequeue();
        }
    }

    /// <summary>Показ закрылся с очисткой: очистить модель отображения, кейкапы и анимации.</summary>
    public event Action? ClearRequested;

    /// <summary>Изменилось состояние или причина (для статуса).</summary>
    public event Action? Changed;

    /// <summary>Фокус сменился (время начала неизвестно: весь дальнейший ввод — новый контекст).</summary>
    public long OnFocusChanged(TimeSpan now) => OnFocusChanged(now, 0);

    /// <summary>
    /// Фокус сменился; <paramref name="contextStart"/> — время (Stopwatch), с которого ввод относится к новому контексту
    /// (0 — неизвестно). Ожидающие переходы прежней неподтверждённой версии отбрасываются. Возвращает версию для проверки.
    /// </summary>
    public long OnFocusChanged(TimeSpan now, long contextStart)
    {
        _past.Add((Version, _currentStart, State));
        if (_past.Count > HistoryDepth)
        {
            _past.RemoveAt(0);
        }

        Discard();
        Version++;
        _currentStart = Math.Max(contextStart, _currentStart);
        Note($"focus v{Version}");
        State = PrivacyState.Checking;
        Reason = PrivacyReason.Checking;
        Deadline = now + ProbeTimeout;
        Changed?.Invoke();
        return Version;
    }

    /// <summary>Результат проверки версии <paramref name="version"/>. Результат устаревшей версии игнорируется.</summary>
    public bool OnProbeResult(long version, ProbeVerdict verdict)
    {
        if (version != Version)
        {
            StaleResults++;
            Note($"stale v{version}:{verdict}");
            return false;
        }

        // Поздний ответ после таймаута той же версии принимается: контекст не менялся (версия та же). Ожидавшие переходы к этому
        // моменту уже отброшены — поздний ответ их не возвращает.
        var (state, reason) = verdict switch
        {
            ProbeVerdict.NotPassword => (PrivacyState.Allowed, PrivacyReason.None),
            ProbeVerdict.Password => (PrivacyState.Protected, PrivacyReason.PasswordField),
            ProbeVerdict.BlockedApp => (PrivacyState.Protected, PrivacyReason.BlockedApp),
            _ => (PrivacyState.Unknown, PrivacyReason.NoData),
        };
        if (Deadline is null && state == State && reason == Reason)
        {
            return true; // перепроверка подтвердила решение: без повторной очистки и уведомлений
        }

        Note($"result v{version}:{verdict}");
        (State, Reason) = (state, reason);
        Deadline = null;
        if (State == PrivacyState.Allowed)
        {
            Release();
        }
        else
        {
            Discard();
            if (State == PrivacyState.Protected && _enabled)
            {
                CloseNow();
            }
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>Проверить срок ожидания: по истечении — «нет данных» (ожидавшие переходы отбрасываются, показ закрыт).</summary>
    public bool Tick(TimeSpan now)
    {
        if (Deadline is { } d && now >= d && State == PrivacyState.Checking)
        {
            Deadline = null;
            State = PrivacyState.Unknown;
            Reason = PrivacyReason.Timeout;
            Timeouts++;
            Discard();
            Changed?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Переходы, переданные после подтверждения обычного поля (в исходном порядке). Вызывающий применяет их к модели отображения
    /// сразу после <see cref="OnProbeResult"/>; повторный вызов возвращает пустой список.
    /// </summary>
    public IReadOnlyList<KeyTransition> TakeReleased()
    {
        if (_released.Count == 0)
        {
            return [];
        }

        var list = _released.ToArray();
        _released.Clear();
        return list;
    }

    /// <summary>
    /// Пропустить ли переход к модели отображения сейчас. false — скрыт или ждёт решения проверки (см. <see cref="PendingCount"/>).
    /// Физическое состояние отслеживается всегда; кнопки мыши проходят.
    /// </summary>
    public bool Filter(KeyTransition t)
    {
        if (Slots.IsMouse(t.KeySlot))
        {
            return true;
        }

        bool down = t.State == KeyState.Down;
        if (down)
        {
            _down.Add(t.KeySlot);
        }
        else
        {
            _down.Remove(t.KeySlot);
        }

        if (!_enabled)
        {
            if (!down)
            {
                _shown.Remove(t.KeySlot);
            }
            else
            {
                _shown.Add(t.KeySlot);
            }

            return true;
        }

        if (!down)
        {
            // Отпускание: скрытая клавиша остаётся скрытой, ожидающая — ждёт вместе со своим нажатием, показанная — отпускается.
            if (_hidden.Remove(t.KeySlot))
            {
                return false;
            }

            if (_pendingDown.Contains(t.KeySlot))
            {
                Pend(t);
                return false;
            }

            if (_shown.Remove(t.KeySlot))
            {
                return true;
            }

            return StateOf(t) == PrivacyState.Allowed;
        }

        if (_hidden.Contains(t.KeySlot))
        {
            return false; // удержание, начатое при закрытом показе: граница — его отпускание
        }

        var state = StateOf(t);
        if (state == PrivacyState.Allowed)
        {
            _shown.Add(t.KeySlot);
            return true;
        }

        if (state == PrivacyState.Checking && IsCurrent(t))
        {
            Pend(t);
            return false;
        }

        _hidden.Add(t.KeySlot);
        return false;
    }

    private bool IsCurrent(KeyTransition t) => t.At == 0 || t.At >= _currentStart;

    /// <summary>Состояние контекста, в котором переход был захвачен (окончательное — для прошлых контекстов).</summary>
    private PrivacyState StateOf(KeyTransition t)
    {
        if (IsCurrent(t))
        {
            return State;
        }

        for (int i = _past.Count - 1; i >= 0; i--)
        {
            if (_past[i].Start > t.At)
            {
                continue;
            }

            // Контексты с одинаковым началом (момент смены был неизвестен) неразличимы: показ — только если все они обычные поля.
            var state = _past[i].Final;
            for (int j = i - 1; j >= 0 && _past[j].Start == _past[i].Start; j--)
            {
                if (_past[j].Final != PrivacyState.Allowed)
                {
                    state = _past[j].Final;
                }
            }

            return state;
        }

        return PrivacyState.Unknown; // старше сохранённой истории — неизвестно, не показывается
    }

    private void Pend(KeyTransition t)
    {
        if (_pending.Count >= MaxPending)
        {
            Discard();
            _hidden.Add(t.KeySlot);
            return;
        }

        _pending.Add(t);
        if (t.State == KeyState.Down)
        {
            _pendingDown.Add(t.KeySlot);
        }
        else
        {
            _pendingDown.Remove(t.KeySlot);
        }
    }

    private void Release()
    {
        if (_pending.Count == 0)
        {
            return;
        }

        foreach (var t in _pending)
        {
            if (t.State == KeyState.Down)
            {
                _shown.Add(t.KeySlot);
            }
            else
            {
                _shown.Remove(t.KeySlot);
            }

            _released.Add(t);
        }

        PendingReleases++;
        Note($"release v{Version}");
        _pending.Clear();
        _pendingDown.Clear();
    }

    /// <summary>Ожидавшие переходы отбрасываются окончательно; нажатые среди них остаются скрытыми до отпускания.</summary>
    private void Discard()
    {
        if (_pending.Count == 0)
        {
            return;
        }

        foreach (int slot in _pendingDown)
        {
            _hidden.Add(slot);
        }

        PendingDiscards++;
        _pending.Clear();
        _pendingDown.Clear();
    }

    private void CloseNow()
    {
        Closures++;
        foreach (int slot in _down)
        {
            _hidden.Add(slot);
        }

        _shown.Clear();
        ClearRequested?.Invoke();
    }
}
