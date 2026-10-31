using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using KeyOverlay.Visual.Windows;

namespace KeyOverlay.Visual.Presentation;

/// <summary>Назначение ручки «Фокуса».</summary>
public enum FocusGripKind
{
    /// <summary>Переместить линзу (или область затемнения).</summary>
    Move,

    /// <summary>Изменить размер линзы (или области затемнения) за правый нижний угол.</summary>
    Resize,

    /// <summary>Переместить область-источник увеличения (только когда она показана).</summary>
    Source,
}

/// <summary>
/// Ручка «Фокуса» (Phase 4F): маленькое окно без активации, которое — в отличие от линзы, рамки и затемнения — принимает мышь.
/// Перетаскивание ручки перемещает или растягивает свой объект; всё остальное по-прежнему пропускает щелчки к приложению под
/// линзой. Видимая зона захвата: значок и подпись на плашке, подсветка и курсор при наведении, другой вид во время перетаскивания.
/// Смещение считается в физических пикселях экрана (указатель), поэтому перенос между мониторами с разным DPI точен.
/// </summary>
public sealed class FocusGrip : NoActivateWindow
{
    private readonly Border _pill;
    private (int X, int Y) _start;
    private bool _dragging;

    public FocusGrip(FocusGripKind kind, string label)
    {
        Kind = kind;
        Title = "Backpack — focus grip";
        IsHitTestVisible = true;
        var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var icon = Icons.Lucide.Icon(kind == FocusGripKind.Resize ? "move-diagonal-2" : kind == FocusGripKind.Source ? "scan" : "move", 14, Brushes.White);
        icon.VerticalAlignment = VerticalAlignment.Center;
        content.Children.Add(icon);
        if (kind != FocusGripKind.Resize)
        {
            content.Children.Add(new TextBlock { Text = label, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 0, 0, 1), VerticalAlignment = VerticalAlignment.Center });
        }

        _pill = new Border
        {
            Child = content,
            CornerRadius = new CornerRadius(kind == FocusGripKind.Resize ? 7 : 9),
            Padding = kind == FocusGripKind.Resize ? new Thickness(5) : new Thickness(9, 4, 11, 4),
            BorderThickness = new Thickness(1.5),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            Background = Idle,
            Margin = new Thickness(4),
            Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 0, Opacity = 0.6 },
        };
        Content = _pill;
        Cursor = kind == FocusGripKind.Resize ? Cursors.SizeNWSE : Cursors.SizeAll;
        ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(this, label);
        SourceInitialized += (_, _) =>
        {
            // NoActivateWindow делает окно прозрачным для мыши; ручке мышь нужна (активации по-прежнему нет).
            Screens.SetExStyle(Hwnd, Screens.ExStyle(Hwnd) & ~Screens.WsExTransparent);
        };
        MouseEnter += (_, _) => Paint();
        MouseLeave += (_, _) => Paint();
        MouseLeftButtonDown += (_, e) =>
        {
            _start = Screens.Cursor();
            _dragging = CaptureMouse();
            Paint();
            if (_dragging)
            {
                e.Handled = true;
                DragStarted?.Invoke(this);
            }
        };
        MouseMove += (_, _) =>
        {
            if (_dragging)
            {
                var (x, y) = Screens.Cursor();
                Dragged?.Invoke(this, x - _start.X, y - _start.Y);
            }
        };
        MouseLeftButtonUp += (_, _) => EndDrag();
        LostMouseCapture += (_, _) => EndDrag();
    }

    private static readonly Brush Idle = Frozen(Color.FromArgb(0xD9, 0x17, 0x1B, 0x1A));
    private static readonly Brush Hover = Frozen(Color.FromArgb(0xF2, 0x0F, 0x7A, 0x5A));
    private static readonly Brush Active = Frozen(Color.FromArgb(0xFF, 0x31, 0xC9, 0x9A));

    public FocusGripKind Kind { get; }

    /// <summary>Идёт перетаскивание.</summary>
    public bool IsDragging => _dragging;

    public event Action<FocusGrip>? DragStarted;

    /// <summary>Смещение указателя от начала перетаскивания (физические пиксели).</summary>
    public event Action<FocusGrip, int, int>? Dragged;

    public event Action<FocusGrip>? DragEnded;

    private void EndDrag()
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        Paint();
        DragEnded?.Invoke(this);
    }

    private void Paint() => _pill.Background = _dragging ? Active : IsMouseOver ? Hover : Idle;

    /// <summary>
    /// Разместить ручку у объекта <paramref name="target"/> (физические пиксели) на мониторе <paramref name="monitor"/>:
    /// перемещение и источник — по центру над верхней кромкой (внутри, если сверху нет места; источник — под нижней кромкой, если
    /// над ней ручка линзы), размер — на правом нижнем углу.
    /// Ручка всегда целиком на мониторе.
    /// </summary>
    public void Place(PixelRect target, MonitorDesc monitor, bool below = false)
    {
        double scale = monitor.Scale;
        _pill.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _pill.DesiredSize;
        int w = (int)Math.Ceiling(size.Width * scale), h = (int)Math.Ceiling(size.Height * scale), gap = (int)Math.Round(2 * scale);
        int x, y;
        if (Kind == FocusGripKind.Resize)
        {
            x = target.Right - (w / 2);
            y = target.Bottom - (h / 2);
        }
        else
        {
            x = target.Left + ((target.Width - w) / 2);
            y = below ? target.Bottom + gap : target.Top - h - gap;
            if (!below && y < monitor.Bounds.Top)
            {
                y = target.Top + gap;
            }
            else if (below && y + h > monitor.Bounds.Bottom)
            {
                y = target.Bottom - h - gap;
            }
        }

        Rect = new PixelRect(x, y, w, h).ClampInto(monitor.Bounds);
        PhysicalPlacement.Place(this, Rect);
        Width = Rect.Width / scale;
        Height = Rect.Height / scale;
        if (!IsVisible)
        {
            Show();
        }
    }

    /// <summary>Где ручка сейчас (физические пиксели).</summary>
    public PixelRect Rect { get; private set; }

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
