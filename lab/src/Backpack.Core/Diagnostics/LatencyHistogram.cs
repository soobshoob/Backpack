using System.Diagnostics;

namespace Backpack.Core.Diagnostics;

/// <summary>
/// Гистограмма с фиксированными корзинами (мкс). Запись — Interlocked, безопасна из потока захвата.
/// Перцентили — верхняя граница корзины (оценка «не больше»), без хранения отдельных значений.
/// </summary>
public sealed class LatencyHistogram
{
    public static readonly long[] BucketUpperMicros =
        [10, 20, 50, 100, 200, 500, 1_000, 2_000, 5_000, 10_000, 16_700, 33_300, 50_000, 100_000, 250_000, 1_000_000, long.MaxValue];

    private readonly long[] _counts = new long[BucketUpperMicros.Length];
    private long _total;
    private long _maxMicros;

    public long Count => Volatile.Read(ref _total);

    public long MaxMicros => Volatile.Read(ref _maxMicros);

    public void RecordTicks(long stopwatchTicks) => RecordMicros(stopwatchTicks * 1_000_000 / Stopwatch.Frequency);

    public void RecordMicros(long micros)
    {
        if (micros < 0)
        {
            micros = 0;
        }

        int i = 0;
        while (micros > BucketUpperMicros[i])
        {
            i++;
        }

        Interlocked.Increment(ref _counts[i]);
        Interlocked.Increment(ref _total);

        long max;
        while (micros > (max = Volatile.Read(ref _maxMicros)) && Interlocked.CompareExchange(ref _maxMicros, micros, max) != max)
        {
        }
    }

    /// <summary>Верхняя граница корзины, в которую попадает перцентиль p (0..1); null — нет данных; long.MaxValue — выше 1 с.</summary>
    public long? PercentileUpperMicros(double p)
    {
        long total = Count;
        if (total == 0)
        {
            return null;
        }

        long rank = (long)Math.Ceiling(p * total);
        long seen = 0;
        for (int i = 0; i < _counts.Length; i++)
        {
            seen += Volatile.Read(ref _counts[i]);
            if (seen >= rank)
            {
                return BucketUpperMicros[i];
            }
        }

        return BucketUpperMicros[^1];
    }

    public LatencyStats ToStats(string definition) => new(
        definition,
        Count,
        PercentileUpperMicros(0.5),
        PercentileUpperMicros(0.95),
        PercentileUpperMicros(0.99),
        Count == 0 ? null : MaxMicros);
}
