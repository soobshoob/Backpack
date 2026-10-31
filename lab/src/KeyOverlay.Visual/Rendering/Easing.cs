namespace KeyOverlay.Visual.Rendering;

/// <summary>CSS cubic-bezier(x1, y1, x2, y2): y по x на [0, 1]. Точки — из Phase 2A (AN-03, ST-03).</summary>
public sealed class CubicBezier
{
    public static readonly CubicBezier EaseOutQuint = new(0.23, 1, 0.32, 1);
    public static readonly CubicBezier EaseInOutExpo = new(0.86, 0, 0.07, 1);

    private readonly double _x1, _y1, _x2, _y2;

    public CubicBezier(double x1, double y1, double x2, double y2) => (_x1, _y1, _x2, _y2) = (x1, y1, x2, y2);

    public double Evaluate(double x)
    {
        if (x <= 0)
        {
            return 0;
        }

        if (x >= 1)
        {
            return 1;
        }

        // Ньютон с откатом на бисекцию: x(t) монотонна при x1, x2 ∈ [0, 1].
        double t = x;
        for (int i = 0; i < 8; i++)
        {
            double err = Sample(_x1, _x2, t) - x;
            if (Math.Abs(err) < 1e-7)
            {
                return Sample(_y1, _y2, t);
            }

            double d = Derivative(_x1, _x2, t);
            if (Math.Abs(d) < 1e-6)
            {
                break;
            }

            t -= err / d;
        }

        double lo = 0, hi = 1;
        t = x;
        for (int i = 0; i < 40; i++)
        {
            double v = Sample(_x1, _x2, t);
            if (Math.Abs(v - x) < 1e-7)
            {
                break;
            }

            if (v < x)
            {
                lo = t;
            }
            else
            {
                hi = t;
            }

            t = (lo + hi) / 2;
        }

        return Sample(_y1, _y2, t);
    }

    private static double Sample(double p1, double p2, double t) =>
        ((((1 - (3 * p2) + (3 * p1)) * t) + ((3 * p2) - (6 * p1))) * t + (3 * p1)) * t;

    private static double Derivative(double p1, double p2, double t) =>
        (3 * (1 - (3 * p2) + (3 * p1)) * t * t) + (2 * ((3 * p2) - (6 * p1)) * t) + (3 * p1);
}
