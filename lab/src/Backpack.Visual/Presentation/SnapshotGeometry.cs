namespace Backpack.Visual.Presentation;

/// <summary>Пропорции окна снимка: в пропорциях снимка, свободное окно со снимком целиком на фоне, свободное с растяжением.</summary>
public enum SnapshotAspect { Keep, Fit, Stretch }

/// <summary>Где появляется новый снимок: на месте прежнего, в углу монитора, поверх снятой области (1:1).</summary>
public enum SnapshotPlace { Last, Corner, Capture }

/// <summary>Состояние снимка: нет снимка, выделение области, обычный вид, развёрнут, скрыт (изображение в памяти).</summary>
public enum SnapshotState { None, Selecting, Normal, Expanded, Hidden }

/// <summary>
/// Пределы размера окна снимка на мониторе (физические пиксели): <see cref="Min"/> — наименьшая сторона, <see cref="MaxWidth"/> и
/// <see cref="MaxHeight"/> — наибольший размер окна в обычном виде (доля рабочей области). Максимум сильнее минимума.
/// </summary>
public readonly record struct SnapshotLimits(int Min, int MaxWidth, int MaxHeight)
{
    /// <param name="work">Рабочая область монитора (физические пиксели).</param>
    /// <param name="scale">Масштаб монитора (DPI / 96).</param>
    /// <param name="minDip">Наименьшая сторона (DIP).</param>
    /// <param name="maxShare">Доля рабочей области 0.2…1.</param>
    public static SnapshotLimits For(PixelRect work, double scale, double minDip, double maxShare)
    {
        double share = Math.Clamp(double.IsFinite(maxShare) ? maxShare : 0.9, 0.05, 1);
        int maxW = Math.Max(1, (int)Math.Floor(work.Width * share)), maxH = Math.Max(1, (int)Math.Floor(work.Height * share));
        int min = Math.Clamp((int)Math.Round(Math.Max(1, minDip) * Math.Max(0.1, scale)), 1, Math.Min(maxW, maxH));
        return new SnapshotLimits(min, maxW, maxH);
    }
}

/// <summary>
/// Геометрия плавающего снимка (физические пиксели виртуального экрана; процесс Per-Monitor V2). Чистые функции без окон — проверяются
/// тестами. Прямоугольник снимка — видимая карточка с изображением (тень и свечение рисуются вокруг неё отдельно).
/// <list type="bullet">
/// <item>в пропорциях снимка высота всегда выводится из ширины (<c>round(w · h₀ / w₀)</c>) — повторные операции не накапливают
///       округление;</item>
/// <item>развёртывание вычисляется из обычного прямоугольника и не меняет его: свёртывание возвращает сохранённое значение
///       как есть, поэтому циклы развернуть/свернуть не дают смещения и роста.</item>
/// </list>
/// </summary>
public static class SnapshotGeometry
{
    /// <summary>Наибольший размер в пропорциях изображения, помещающийся в коробку.</summary>
    public static (int W, int H) FitInto(int imageW, int imageH, int boxW, int boxH)
    {
        imageW = Math.Max(1, imageW);
        imageH = Math.Max(1, imageH);
        double s = Math.Min((double)Math.Max(1, boxW) / imageW, (double)Math.Max(1, boxH) / imageH);
        return (Math.Max(1, (int)Math.Round(imageW * s)), Math.Max(1, (int)Math.Round(imageH * s)));
    }

    /// <summary>Высота для ширины <paramref name="w"/> в пропорциях изображения.</summary>
    public static int HeightFor(int w, int imageW, int imageH) => Math.Max(1, (int)Math.Round(w * (double)Math.Max(1, imageH) / Math.Max(1, imageW)));

    /// <summary>
    /// Размер в пределах. В пропорциях снимка (<paramref name="keepAspect"/>) решает ширина: окно равномерно увеличивается, пока
    /// меньшая сторона не станет не меньше минимума, и уменьшается, пока не поместится в максимум (максимум сильнее). Свободное
    /// окно ограничивается по каждой оси отдельно.
    /// </summary>
    public static (int W, int H) Limit(int w, int h, int imageW, int imageH, bool keepAspect, SnapshotLimits l)
    {
        if (!keepAspect)
        {
            return (Math.Clamp(w, Math.Min(l.Min, l.MaxWidth), l.MaxWidth), Math.Clamp(h, Math.Min(l.Min, l.MaxHeight), l.MaxHeight));
        }

        double ww = Math.Max(1, w), hh = ww * Math.Max(1, imageH) / Math.Max(1, imageW);
        double grow = Math.Max(l.Min / ww, l.Min / hh);
        if (grow > 1)
        {
            ww *= grow;
        }

        hh = ww * Math.Max(1, imageH) / Math.Max(1, imageW);
        double shrink = Math.Min(l.MaxWidth / ww, l.MaxHeight / hh);
        if (shrink < 1)
        {
            ww *= shrink;
        }

        int iw = Math.Max(1, (int)Math.Floor(ww + 0.5));
        int ih = HeightFor(iw, imageW, imageH);
        while ((iw > l.MaxWidth || ih > l.MaxHeight) && iw > 1)
        {
            iw--; // округление высоты не выводит за максимум
            ih = HeightFor(iw, imageW, imageH);
        }

        return (iw, ih);
    }

    /// <summary>
    /// Прямоугольник нового снимка. <paramref name="capture"/> — снятая область, <paramref name="last"/> — прежнее место (обычный
    /// вид), <paramref name="work"/> и <paramref name="scale"/> — монитор, где снимок появится. В углу снимок не растягивается больше
    /// исходного размера (1 пиксель снимка — 1 пиксель экрана), чтобы не терять резкость.
    /// </summary>
    public static PixelRect Start(SnapshotPlace place, string corner, int imageW, int imageH, PixelRect capture, PixelRect? last, PixelRect work, double scale,
        double startSizeDip, double edgeMarginDip, bool keepAspect, SnapshotLimits limits)
    {
        if (place == SnapshotPlace.Capture)
        {
            var (cw, ch) = Limit(imageW, imageH, imageW, imageH, true, limits);
            return new PixelRect(capture.Left, capture.Top, cw, ch).ClampInto(work);
        }

        if (place == SnapshotPlace.Last && last is { IsEmpty: false } l)
        {
            var (lw, lh) = Limit(l.Width, l.Height, imageW, imageH, keepAspect, limits);
            return new PixelRect(l.Left, l.Top, lw, lh).ClampInto(work);
        }

        int target = Math.Min((int)Math.Round(Math.Max(1, startSizeDip) * scale), Math.Max(imageW, imageH));
        int w = imageW >= imageH ? target : Math.Max(1, (int)Math.Round(target * (double)imageW / Math.Max(1, imageH)));
        var (sw, sh) = Limit(w, 0, imageW, imageH, true, limits);
        return InCorner(sw, sh, corner, work, (int)Math.Round(Math.Max(0, edgeMarginDip) * scale));
    }

    /// <summary>Прямоугольник размера w×h в углу рабочей области с отступом <paramref name="margin"/> (физические пиксели).</summary>
    public static PixelRect InCorner(int w, int h, string corner, PixelRect work, int margin)
    {
        bool left = corner is "topLeft" or "bottomLeft", bottom = corner is "bottomLeft" or "bottomRight";
        int x = left ? work.Left + margin : work.Right - margin - w;
        int y = bottom ? work.Bottom - margin - h : work.Top + margin;
        return new PixelRect(x, y, w, h).ClampInto(work);
    }

    /// <summary>
    /// Развёрнутый снимок: в пропорциях изображения, по центру рабочей области <paramref name="work"/> за вычетом отступа
    /// <paramref name="marginDip"/> (при отступе 48 DIP на 1920×1040 — около 95 % ширины и 90 % высоты). Без
    /// <paramref name="upscale"/> не больше исходного размера, но не меньше текущего обычного вида. Обычный прямоугольник не меняется.
    /// </summary>
    public static PixelRect Expanded(PixelRect normal, int imageW, int imageH, PixelRect work, double scale, double marginDip, bool upscale)
    {
        int m = (int)Math.Round(Math.Max(0, marginDip) * scale);
        var box = new PixelRect(work.Left + m, work.Top + m, work.Width - (2 * m), work.Height - (2 * m));
        if (box.Width < 16 || box.Height < 16)
        {
            box = work;
        }

        var (w, h) = FitInto(imageW, imageH, box.Width, box.Height);
        if (!upscale && w > imageW)
        {
            // Не больше исходного размера — но и не меньше того, что уже видно в обычном виде.
            double current = Math.Max(1, Math.Max((double)normal.Width / Math.Max(1, imageW), (double)normal.Height / Math.Max(1, imageH)));
            int cw = (int)Math.Round(imageW * current);
            if (cw < w)
            {
                w = cw;
                h = HeightFor(w, imageW, imageH);
            }
        }

        return new PixelRect(box.Left + ((box.Width - w) / 2), box.Top + ((box.Height - h) / 2), w, h);
    }

    /// <summary>
    /// Изменение размера ручкой правого нижнего угла: левый верхний угол на месте. В пропорциях снимка смещение указателя
    /// проецируется на диагональ окна (плавно в любом направлении); размер ограничен пределами и краем <paramref name="bounds"/>.
    /// </summary>
    public static PixelRect Resize(PixelRect start, int dx, int dy, int imageW, int imageH, bool keepAspect, SnapshotLimits limits, PixelRect bounds)
    {
        var room = limits with
        {
            MaxWidth = Math.Max(1, Math.Min(limits.MaxWidth, bounds.Right - start.Left)),
            MaxHeight = Math.Max(1, Math.Min(limits.MaxHeight, bounds.Bottom - start.Top)),
        };
        if (!keepAspect)
        {
            var (fw, fh) = Limit(start.Width + dx, start.Height + dy, imageW, imageH, false, room);
            return start with { Width = fw, Height = fh };
        }

        double d2 = ((double)start.Width * start.Width) + ((double)start.Height * start.Height);
        double s = 1 + (((dx * (double)start.Width) + (dy * (double)start.Height)) / Math.Max(1, d2));
        var (w, h) = Limit((int)Math.Round(start.Width * Math.Max(0.01, s)), 0, imageW, imageH, true, room);
        return start with { Width = w, Height = h };
    }

    /// <summary>Перемещение на (dx, dy) в пределах <paramref name="bounds"/> (объединение мониторов) — размер не меняется.</summary>
    public static PixelRect Move(PixelRect start, int dx, int dy, PixelRect bounds) =>
        (start with { Left = start.Left + dx, Top = start.Top + dy }).ClampInto(bounds);

    /// <summary>
    /// Перенос на монитор с другим масштабом: размер в DIP сохраняется (физический × <paramref name="ratio"/>) вокруг центра, затем
    /// ограничивается пределами нового монитора и его рабочей областью.
    /// </summary>
    public static PixelRect Rescale(PixelRect r, double ratio, int imageW, int imageH, bool keepAspect, SnapshotLimits limits, PixelRect work)
    {
        int w = Math.Max(1, (int)Math.Round(r.Width * ratio)), h = Math.Max(1, (int)Math.Round(r.Height * ratio));
        (w, h) = Limit(w, h, imageW, imageH, keepAspect, limits);
        return new PixelRect(r.Left + ((r.Width - w) / 2), r.Top + ((r.Height - h) / 2), w, h).ClampInto(work);
    }

    /// <summary>Обычный прямоугольник на мониторе: в пределах размера и целиком в рабочей области.</summary>
    public static PixelRect Fit(PixelRect r, int imageW, int imageH, bool keepAspect, SnapshotLimits limits, PixelRect work)
    {
        var (w, h) = Limit(r.Width, r.Height, imageW, imageH, keepAspect, limits);
        return new PixelRect(r.Left, r.Top, w, h).ClampInto(work);
    }

    /// <summary>Точка внутри прямоугольника (правая и нижняя границы не входят).</summary>
    public static bool Contains(PixelRect r, (int X, int Y) p) => p.X >= r.Left && p.X < r.Right && p.Y >= r.Top && p.Y < r.Bottom;
}
