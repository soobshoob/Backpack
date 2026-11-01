namespace Backpack.Visual.Presentation;

/// <summary>
/// Состояние инструментов презентации (Phase 2F). Обычный режим — все окна Backpack click-through и no-activate.
/// Мышь принимают только: окно выбора области линзы (пока идёт выбор) и окно рисования (пока активен режим рисования и не идёт
/// выбор области). Радиальное меню — часть режима рисования.
/// <list type="table">
/// <listheader><term>Совместно</term><description>Правило</description></listheader>
/// <item><term>Линза + рисование</term><description>допустимо: линза только показывает (click-through), рисунок поверх неё</description></item>
/// <item><term>Выбор области + рисование</term><description>рисование приостанавливает приём мыши на время выбора и возобновляет после</description></item>
/// <item><term>Выбор области + линза</term><description>нет: новая линза заменяет прежнюю после выбора; повторная команда во время выбора — отмена</description></item>
/// <item><term>Радиальное меню</term><description>только в режиме рисования (команда в другом состоянии сначала включает рисование)</description></item>
/// </list>
/// Выход: Esc закрывает верхний слой (меню → выбор → рисование → линза), по одному за нажатие; повторная команда закрывает
/// свой режим; скрытие отображения и потеря контекста (блокировка, смена рабочего стола, сон) закрывают всё.
/// </summary>
public enum PresentationLayer { Radial, Selecting, Drawing, Lens }

public enum PresentationCommand { Lens, Draw, Radial, Escape, ExitAll, SelectionDone, SelectionCancelled, LensExpired }

public sealed record PresentationState(bool Selecting, bool Lens, bool Drawing, bool Radial)
{
    public static PresentationState Normal { get; } = new(false, false, false, false);

    public bool IsNormal => this == Normal;

    /// <summary>Есть окно, которое сейчас принимает мышь (выбор области или рисование, если не приостановлено выбором).</summary>
    public bool CapturesMouse => Selecting || Drawing;

    /// <summary>Рисование принимает мышь (не приостановлено выбором области).</summary>
    public bool DrawingInteractive => Drawing && !Selecting;

    public PresentationLayer? Top => Radial ? PresentationLayer.Radial : Selecting ? PresentationLayer.Selecting : Drawing ? PresentationLayer.Drawing : Lens ? PresentationLayer.Lens : null;
}

/// <summary>Чистые переходы (без окон) — проверяются тестами на все пары состояние × команда.</summary>
public static class PresentationModes
{
    public static PresentationState Next(PresentationState s, PresentationCommand c) => c switch
    {
        PresentationCommand.ExitAll => PresentationState.Normal,
        PresentationCommand.Escape => s.Top switch
        {
            PresentationLayer.Radial => s with { Radial = false },
            PresentationLayer.Selecting => s with { Selecting = false },
            PresentationLayer.Drawing => s with { Drawing = false },
            PresentationLayer.Lens => s with { Lens = false },
            _ => s,
        },
        PresentationCommand.Lens => s.Selecting ? s with { Selecting = false }
            : s.Lens ? s with { Lens = false }
            : s with { Selecting = true, Radial = false },
        PresentationCommand.SelectionDone => s.Selecting ? s with { Selecting = false, Lens = true } : s,
        PresentationCommand.SelectionCancelled => s with { Selecting = false },
        PresentationCommand.LensExpired => s with { Lens = false },
        PresentationCommand.Draw => s.Drawing ? s with { Drawing = false, Radial = false } : s with { Drawing = true, Selecting = false },
        PresentationCommand.Radial => s.Radial ? s with { Radial = false } : s with { Drawing = true, Radial = true, Selecting = false },
        _ => s,
    };
}
