namespace Backpack.Core.Privacy;

/// <summary>
/// Когда динамическая приватность обращается к приложению с фокусом (UI Automation). Каждая проверка — вызовы в UI-поток
/// этого приложения (WM_GETOBJECT, свойства элемента), поэтому проверки делаются только по событиям и ограниченное число раз.
/// <para>
/// <b>Исправление 0.4.0-preview.8.</b> Раньше ответ «нет данных» (элемент не сообщает IsPassword — так отвечает, например,
/// 3D-вид CAD-программы, в отличие от обычного поля ввода размера) перепроверялся каждые 1,5 с <i>бесконечно</i>, пока фокус
/// оставался в этом элементе: Backpack всё время, пока пользователь работал в 3D-виде, обращался к UI-потоку программы. Поле ввода
/// отвечает «не пароль», и обращения прекращались — поэтому поведение в поле и в 3D-виде различалось. Теперь после события фокуса
/// выполняются только <see cref="RetryDelaysMs"/> перепроверок, дальше — ожидание следующего события, без опроса.
/// </para>
/// </summary>
public static class FocusProbePolicy
{
    /// <summary>Задержки перепроверки запрещающего ответа той же версии (UIA может ещё отдавать прежний элемент), мс.</summary>
    public static IReadOnlyList<int> RetryDelaysMs { get; } = [120, 400, 1200];

    /// <summary>Пауза перед подтверждающей пробой разрешающего ответа, мс.</summary>
    public const int ConfirmDelayMs = 80;

    /// <summary>
    /// Сколько ждать следующей перепроверки той же версии после <paramref name="retriesDone"/> перепроверок (бесконечно — до
    /// следующего события). Ответ не влияет: «нет данных» больше не опрашивается периодически.
    /// </summary>
    public static int NextWaitMs(int retriesDone) => retriesDone >= 0 && retriesDone < RetryDelaysMs.Count ? RetryDelaysMs[retriesDone] : Timeout.Infinite;

    /// <summary>Сколько проверок одной версии самое большее: первая (с подтверждением) и перепроверки.</summary>
    public static int MaxProbesPerFocusChange => 1 + RetryDelaysMs.Count;
}

/// <summary>Источник уведомления о фокусе.</summary>
public enum FocusSource : byte { WinEvent = 0, UiAutomation = 1 }

/// <summary>
/// Отсев повторных уведомлений о фокусе на <i>том же</i> элементе. Некоторые программы (CAD, редакторы с собственной отрисовкой)
/// повторно объявляют фокус на уже сфокусированном элементе при работе с клавиатурой; каждое такое уведомление раньше начинало
/// новую версию контекста — показ закрывался до проверки, и Backpack снова обращался к программе через UI Automation. Тот же
/// элемент отвечает так же, поэтому повтор от того же источника в течение <see cref="BurstWindow"/> после предыдущего такого же
/// уведомления пропускается. Новый элемент от любого источника сбрасывает память обоих источников; смена активного окна,
/// неизвестный элемент или пауза дольше окна — обычное уведомление. Потокобезопасно (WinEvent и UIA приходят в разных потоках).
/// Чистая логика: время задаёт вызывающий.
/// </summary>
public sealed class FocusNoiseFilter
{
    /// <summary>Окно, в котором повтор того же элемента считается шумом.</summary>
    public static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(1);

    private readonly Lock _lock = new();
    private readonly string?[] _lastKey = new string?[2];
    private readonly TimeSpan[] _lastAt = new TimeSpan[2];

    public long Suppressed { get; private set; }

    /// <summary>Смена активного окна: всегда новый контекст; повторы прежних элементов больше не узнаются.</summary>
    public void OnForegroundChanged()
    {
        lock (_lock)
        {
            _lastKey[0] = _lastKey[1] = null;
        }
    }

    /// <summary>
    /// Уведомление о фокусе на элементе с ключом <paramref name="key"/> (null — элемент неизвестен: всегда принимается).
    /// true — новый контекст (проверить), false — повтор того же элемента (пропустить).
    /// </summary>
    public bool Accept(FocusSource source, string? key, TimeSpan now)
    {
        int i = (int)source;
        lock (_lock)
        {
            if (key is not null && key == _lastKey[i] && now - _lastAt[i] < BurstWindow)
            {
                _lastAt[i] = now; // продолжающийся поток повторов остаётся шумом
                Suppressed++;
                return false;
            }

            _lastKey[0] = _lastKey[1] = null; // новый элемент: прежняя память обоих источников больше не действует
            _lastKey[i] = key;
            _lastAt[i] = now;
            return true;
        }
    }

    /// <summary>Ключ элемента события WinEvent (окно, объект, дочерний элемент).</summary>
    public static string WinEventKey(nint hwnd, int idObject, int idChild) => $"w:{hwnd:X}:{idObject}:{idChild}";

    /// <summary>Ключ элемента UI Automation по RuntimeId (null — неизвестен).</summary>
    public static string? RuntimeIdKey(int[]? runtimeId) => runtimeId is { Length: > 0 } r ? "u:" + string.Join('.', r) : null;
}
