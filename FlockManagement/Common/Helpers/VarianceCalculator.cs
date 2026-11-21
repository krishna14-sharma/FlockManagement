using System;
using FlockManagement.Common.Constants;

namespace FlockManagement.Common.Helpers
{
    /// <summary>Result of comparing an actual value with its standard.</summary>
    public class VarianceResult
    {
        public decimal? Standard { get; set; }
        public decimal? Actual { get; set; }
        public decimal? Variance { get; set; }
        public decimal? VariancePct { get; set; }
        public string StatusCode { get; set; }
    }

    /// <summary>
    /// Central Standard-vs-Actual logic used by every analysis screen.
    /// Variance = Actual - Standard; Variance % = (Actual - Standard) / Standard x 100.
    /// Status precedence: explicit critical band -> explicit min/max band -> tolerance %.
    /// </summary>
    public static class VarianceCalculator
    {
        public static VarianceResult Evaluate(decimal? standard, decimal? actual,
            decimal? min, decimal? max, decimal? criticalMin, decimal? criticalMax,
            decimal warningPct, decimal criticalPct, char direction)
        {
            var r = new VarianceResult { Standard = standard, Actual = actual };
            if (!standard.HasValue || !actual.HasValue)
            {
                r.StatusCode = StatusCodes.NoData;
                return r;
            }

            r.Variance = actual.Value - standard.Value;
            if (standard.Value != 0m)
                r.VariancePct = Math.Round(r.Variance.Value / standard.Value * 100m, 2, MidpointRounding.AwayFromZero);

            var a = actual.Value;
            var isLow = a < standard.Value;
            var absPct = r.VariancePct.HasValue ? Math.Abs(r.VariancePct.Value) : 0m;

            // Deviation in the "good" direction is never critical for one-sided parameters
            var favourable = (direction == 'H' && !isLow) || (direction == 'L' && isLow);

            if (!favourable)
            {
                if ((criticalMin.HasValue && a < criticalMin.Value) || (criticalMax.HasValue && a > criticalMax.Value))
                {
                    r.StatusCode = StatusCodes.Critical;
                    return r;
                }
            }

            if (min.HasValue || max.HasValue)
            {
                var belowMin = min.HasValue && a < min.Value;
                var aboveMax = max.HasValue && a > max.Value;
                if (!belowMin && !aboveMax)
                {
                    r.StatusCode = StatusCodes.Within;
                    return r;
                }
                if (!favourable && !criticalMin.HasValue && !criticalMax.HasValue && absPct > criticalPct)
                {
                    r.StatusCode = StatusCodes.Critical;
                    return r;
                }
                r.StatusCode = belowMin ? StatusCodes.Below : StatusCodes.Above;
                return r;
            }

            if (absPct <= warningPct)
                r.StatusCode = StatusCodes.Within;
            else if (!favourable && absPct > criticalPct)
                r.StatusCode = StatusCodes.Critical;
            else
                r.StatusCode = isLow ? StatusCodes.Below : StatusCodes.Above;
            return r;
        }
    }
}
