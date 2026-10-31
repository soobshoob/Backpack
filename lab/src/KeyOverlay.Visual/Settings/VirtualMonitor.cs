using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KeyOverlay.Visual.Settings;

/// <summary>
/// Виртуальный монитор превью (Phase 4G): границы области превью — границы монитора. Логический размер монитора — размер области,
/// делённый на масштаб (база <see cref="BaseSize"/> вписывается в область, масштаб ограничен <see cref="MinScale"/>…
/// <see cref="MaxScale"/>), поэтому вертикальное превью — вертикальный монитор, горизонтальное — горизонтальный, а элементы
/// остаются крупными. Сцена строится заново при изменении логического размера (позиции считаются от его границ) и выводится
/// одним <see cref="Viewbox"/> без полей: пропорции сцены совпадают с пропорциями области.
/// </summary>
public sealed class VirtualMonitor : Decorator
{
    private readonly Func<Size, FrameworkElement> _build;
    private readonly Viewbox _view = new() { Stretch = Stretch.Uniform };

    public VirtualMonitor(Func<Size, FrameworkElement> build, Size baseSize, double minScale, double maxScale)
    {
        _build = build;
        BaseSize = baseSize;
        MinScale = minScale;
        MaxScale = maxScale;
        Child = _view;
    }

    public Size BaseSize { get; }

    public double MinScale { get; }

    public double MaxScale { get; }

    /// <summary>Логический размер текущей сцены (пусто — ещё не построена).</summary>
    public Size Logical { get; private set; } = Size.Empty;

    /// <summary>Масштаб сцены относительно области.</summary>
    public static double ScaleFor(Size area, Size baseSize, double min, double max) =>
        Math.Clamp(Math.Min(area.Width / baseSize.Width, area.Height / baseSize.Height), min, max);

    public static Size LogicalFor(Size area, Size baseSize, double min, double max)
    {
        double k = ScaleFor(area, baseSize, min, max);
        return new Size(area.Width / k, area.Height / k);
    }

    /// <summary>Построить сцену для области <paramref name="area"/> (раскладка вызывает сама; тесты — напрямую).</summary>
    public void Rebuild(Size area)
    {
        if (!(area.Width > 1) || !(area.Height > 1) || double.IsInfinity(area.Width) || double.IsInfinity(area.Height))
        {
            area = BaseSize;
        }

        var logical = LogicalFor(area, BaseSize, MinScale, MaxScale);
        if (!Logical.IsEmpty && Math.Abs(logical.Width - Logical.Width) < 1 && Math.Abs(logical.Height - Logical.Height) < 1)
        {
            return;
        }

        Logical = logical;
        var scene = _build(logical);
        scene.Width = logical.Width;
        scene.Height = logical.Height;
        _view.Child = scene;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        Rebuild(constraint);
        _view.Measure(constraint);
        return new Size(double.IsInfinity(constraint.Width) ? _view.DesiredSize.Width : constraint.Width,
            double.IsInfinity(constraint.Height) ? _view.DesiredSize.Height : constraint.Height);
    }
}
