namespace KeyOverlay.Lab.Core.Model;

/// <summary>
/// Контекст сверки. <c>null</c> — проверить не удалось (считается недоверенным).
/// Проверки выполняются в UI-потоке, не в callback хука.
/// </summary>
public readonly record struct ProbeContext(bool SessionLocked, bool? InputDesktopIsOurs, bool? ForegroundIntegrityNotHigher)
{
    /// <summary>Проектная эвристика из Phase 1A, а не гарантия Win32.</summary>
    public bool IsTrusted => !SessionLocked && InputDesktopIsOurs == true && ForegroundIntegrityNotHigher == true;
}

public interface IKeyStateProbe
{
    ProbeContext GetContext();

    /// <summary>true — старший бит GetAsyncKeyState установлен. false — ноль: «отпущена» или «вызов не удался», неразличимо.</summary>
    bool IsDown(ushort vk);
}

public enum ReconcileVerdict
{
    NoCandidates,
    Untrusted,
    NoPositiveControl,
    Applied,
}

public sealed class ReconcileCounters
{
    public long Runs { get; set; }
    public long Untrusted { get; set; }
    public long NoPositiveControl { get; set; }
    public long AmbiguousVk { get; set; }
    public long ConfirmedDown { get; set; }
}

/// <summary>
/// Сверка состояния модели с GetAsyncKeyState. Правила (эвристика, подлежит проверке в X3):
/// <list type="number">
/// <item>Контекст недоверенный → ничего не менять.</item>
/// <item>Ноль ничего не доказывает, пока в том же цикле нет положительного контроля:
///       хотя бы один зондированный VK вернул «нажата» (при ошибке функция возвращает 0, а не ненулевое значение).</item>
/// <item>«Нажата» нельзя приписать конкретному слоту, если тот же VK есть у нескольких ненажатых-неотпущенных слотов
///       (разные устройства или разные физические клавиши с одним VK) — неопределённость сохраняется.</item>
/// <item>Ноль при доверенном контексте и положительном контроле относится ко всем слотам с этим VK
///       (агрегированное утверждение «ни одна клавиша с этим VK не нажата»), без различения устройств.</item>
/// <item>Слоты с событиями в окне тишины не трогаются (защита от гонки с ещё не применёнными событиями).</item>
/// </list>
/// </summary>
public static class Reconciliation
{
    public static ReconcileVerdict Run(SourceModel model, IKeyStateProbe probe, long now, long quietTicks, ReconcileCounters counters, List<KeyTransition> output)
    {
        var all = model.NonUpSlots().Where(c => c.ProbeVk != 0).ToList();
        var candidates = all.Where(c => now - c.LastActivity >= quietTicks).ToList();
        if (candidates.Count == 0)
        {
            return ReconcileVerdict.NoCandidates;
        }

        counters.Runs++;
        var context = probe.GetContext();
        if (!context.IsTrusted)
        {
            counters.Untrusted++;
            return ReconcileVerdict.Untrusted;
        }

        // Зондируются VK всех ненажатых-неотпущенных слотов, включая свежие: они служат положительным контролем.
        var results = new Dictionary<ushort, bool>();
        foreach (var vk in all.Select(c => c.ProbeVk).Distinct())
        {
            results[vk] = probe.IsDown(vk);
        }

        bool positiveControl = results.Values.Any(v => v);
        var slotsPerVk = all.GroupBy(c => c.ProbeVk).ToDictionary(g => g.Key, g => g.Count());

        foreach (var c in candidates)
        {
            bool down = results[c.ProbeVk];
            if (down)
            {
                if (c.State == KeyState.Down)
                {
                    counters.ConfirmedDown++;
                }
                else if (slotsPerVk[c.ProbeVk] == 1)
                {
                    model.ApplyReconcile(c.Index, KeyState.Down, output);
                }
                else
                {
                    counters.AmbiguousVk++;
                }
            }
            else if (positiveControl)
            {
                model.ApplyReconcile(c.Index, KeyState.Up, output);
            }
        }

        if (!positiveControl)
        {
            counters.NoPositiveControl++;
            return ReconcileVerdict.NoPositiveControl;
        }

        return ReconcileVerdict.Applied;
    }
}
