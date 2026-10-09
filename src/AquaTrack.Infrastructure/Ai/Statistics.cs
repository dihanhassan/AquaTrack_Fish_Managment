namespace AquaTrack.Infrastructure.Ai;

/// <summary>
/// The small amount of maths the AI module needs. Kept dependency-free and deterministic so the
/// predictions can be explained to a farmer rather than coming out of a black box.
/// </summary>
public static class Statistics
{
    public readonly record struct LinearFit(double Slope, double Intercept, double RSquared)
    {
        public double PredictAt(double x) => Slope * x + Intercept;
    }

    /// <summary>Ordinary least squares over (x, y) pairs. Returns null when there is nothing to fit.</summary>
    public static LinearFit? Fit(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count < 2) return null;

        var n = points.Count;
        var meanX = points.Average(p => p.X);
        var meanY = points.Average(p => p.Y);

        var covariance = points.Sum(p => (p.X - meanX) * (p.Y - meanY));
        var varianceX = points.Sum(p => (p.X - meanX) * (p.X - meanX));

        // All samples share one x value, so no line can be fitted through them.
        if (Math.Abs(varianceX) < 1e-9) return null;

        var slope = covariance / varianceX;
        var intercept = meanY - slope * meanX;

        var totalSumSquares = points.Sum(p => (p.Y - meanY) * (p.Y - meanY));
        var residualSumSquares = points.Sum(p =>
        {
            var predicted = slope * p.X + intercept;
            return (p.Y - predicted) * (p.Y - predicted);
        });

        var rSquared = totalSumSquares < 1e-9 ? 1d : 1d - residualSumSquares / totalSumSquares;
        return new LinearFit(slope, intercept, Math.Clamp(rSquared, 0d, 1d));
    }

    public static double Mean(IReadOnlyCollection<double> values)
        => values.Count == 0 ? 0d : values.Average();

    /// <summary>Sample standard deviation (n-1), which is the right one for a window of observations.</summary>
    public static double StdDev(IReadOnlyCollection<double> values)
    {
        if (values.Count < 2) return 0d;

        var mean = values.Average();
        var sumSquares = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSquares / (values.Count - 1));
    }

    /// <summary>
    /// How many standard deviations <paramref name="value"/> sits from the mean of the baseline.
    /// Returns 0 when the baseline is too small or flat to judge against.
    /// </summary>
    public static double ZScore(double value, IReadOnlyCollection<double> baseline)
    {
        var sd = StdDev(baseline);
        if (sd < 1e-9) return 0d;

        return (value - Mean(baseline)) / sd;
    }

    /// <summary>Simple moving average of the final <paramref name="window"/> observations.</summary>
    public static double MovingAverage(IReadOnlyList<double> series, int window)
    {
        if (series.Count == 0) return 0d;

        var take = Math.Min(window, series.Count);
        return series.Skip(series.Count - take).Take(take).Average();
    }
}
