using System;
using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// Step 6: checks the sampling and compares it with the header. Data is never changed:
    /// there is no resampling, only report entries (D1, D14).
    /// </summary>
    public class SamplingCheckStep : IConditioningStep
    {
        /// <summary>A step larger than this many median steps is reported as a gap (D14).</summary>
        public const double GapFactor = 1.5;

        /// <summary>Header STEP may differ from the median step by this fraction before a warning.</summary>
        public const double StepTolerance = 0.01;

        /// <summary>Gaps reported one by one; after that only a total is reported.</summary>
        private const int MaxDetailedGaps = 20;

        public List<ReportEntry> Apply(Well well)
        {
            ReportLog log = new ReportLog(well.FileName, Stage.Condition);
            double[] depth = well.Depth;

            if (!well.IsDepthIndexed || depth.Length < 2)
            {
                return log.Entries;
            }

            string unit = well.DepthUnit == "m" ? " m" : " (unit unknown)";
            double medianStep = MedianStep(depth);

            CheckHeaderStep(well, medianStep, unit, log);
            CheckHeaderRange(well, medianStep, unit, log);
            CheckGaps(depth, medianStep, unit, log);
            return log.Entries;
        }

        /// <summary>Median of the steps between rows. After steps 4 and 5 every step is positive.</summary>
        private static double MedianStep(double[] depth)
        {
            double[] steps = new double[depth.Length - 1];
            for (int i = 1; i < depth.Length; i++)
            {
                steps[i - 1] = depth[i] - depth[i - 1];
            }
            Array.Sort(steps);
            int middle = steps.Length / 2;
            return steps.Length % 2 == 1 ? steps[middle] : (steps[middle - 1] + steps[middle]) / 2.0;
        }

        private static void CheckHeaderStep(Well well, double medianStep, string unit, ReportLog log)
        {
            double headerStep = Math.Abs(well.HeaderStep);
            if (double.IsNaN(headerStep))
            {
                log.Add(Severity.Warning, "STEP missing or not a number; sampling not compared with the header", 0, "~W");
                return;
            }
            if (headerStep == 0)
            {
                // In LAS, STEP 0 means the sampling is irregular, so there is nothing to compare.
                return;
            }
            if (Math.Abs(medianStep - headerStep) > StepTolerance * headerStep)
            {
                log.Add(Severity.Warning,
                    $"Header STEP is {Format(headerStep)}{unit} but the median step in the data is {Format(medianStep)}{unit}",
                    well.StepItem != null ? well.StepItem.line : 0, "~W");
            }
        }

        /// <summary>STRT/STOP may be written in either order (logs recorded upward), so min and max are compared.</summary>
        private static void CheckHeaderRange(Well well, double medianStep, string unit, ReportLog log)
        {
            if (double.IsNaN(well.HeaderStart) || double.IsNaN(well.HeaderStop))
            {
                log.Add(Severity.Warning, "STRT or STOP missing or not a number; depth range not compared with the header", 0, "~W");
                return;
            }

            double headerTop = Math.Min(well.HeaderStart, well.HeaderStop);
            double headerBottom = Math.Max(well.HeaderStart, well.HeaderStop);
            double dataTop = well.Depth[0];
            double dataBottom = well.Depth[well.Depth.Length - 1];
            double tolerance = medianStep / 2.0;

            if (Math.Abs(headerTop - dataTop) > tolerance || Math.Abs(headerBottom - dataBottom) > tolerance)
            {
                log.Add(Severity.Warning,
                    $"Header STRT/STOP give {Format(headerTop)} to {Format(headerBottom)}{unit} but the data covers {Format(dataTop)} to {Format(dataBottom)}{unit}",
                    0, "~W");
            }
        }

        private static void CheckGaps(double[] depth, double medianStep, string unit, ReportLog log)
        {
            int gapCount = 0;
            for (int i = 1; i < depth.Length; i++)
            {
                double step = depth[i] - depth[i - 1];
                if (step > GapFactor * medianStep)
                {
                    gapCount++;
                    if (gapCount <= MaxDetailedGaps)
                    {
                        log.Add(Severity.Warning,
                            $"Gap of {Format(step)}{unit} between depth {Format(depth[i - 1])} and {Format(depth[i])}{unit}",
                            0, "~A");
                    }
                }
            }

            if (gapCount > MaxDetailedGaps)
            {
                log.Add(Severity.Warning,
                    $"{gapCount} gaps larger than {GapFactor} x median step in total (first {MaxDetailedGaps} listed)",
                    0, "~A");
            }
        }

        private static string Format(double value)
        {
            return LasText.FormatNumber(Math.Round(value, 4));
        }
    }
}
