namespace Gorm.Application.Intelligence.Algorithms.Abstractions;

internal static class GraphAlgorithmNumeric
{
    private const double DefaultTolerance = 1e-12;

    public static bool IsZero(double value, double tolerance = DefaultTolerance) =>
        double.IsFinite(value) && Math.Abs(value) <= tolerance;

    public static bool AreEqual(double left, double right, double tolerance = DefaultTolerance)
    {
        if (!double.IsFinite(left) || !double.IsFinite(right))
        {
            return (double.IsPositiveInfinity(left) && double.IsPositiveInfinity(right)) ||
                   (double.IsNegativeInfinity(left) && double.IsNegativeInfinity(right));
        }

        var scale = Math.Max(1, Math.Max(Math.Abs(left), Math.Abs(right)));
        return Math.Abs(left - right) <= tolerance * scale;
    }
}