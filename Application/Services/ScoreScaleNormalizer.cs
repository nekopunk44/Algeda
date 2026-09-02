namespace Application.Services
{
    internal static class ScoreScaleNormalizer
    {
        public static double ToFivePointScale(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0d;

            var normalized = value > 5d && value <= 100d
                ? value / 20d
                : value;

            return Math.Round(Math.Clamp(normalized, 0d, 5d), 2, MidpointRounding.AwayFromZero);
        }
    }
}
