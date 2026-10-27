using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// Runs the conditioning steps in the order given in DESIGN §5.
    /// Step 1 (NULL to NaN) already happened in the reader (D3).
    /// </summary>
    public static class Conditioner
    {
        /// <summary>Runs every step on the well. Stops early if a step rejects the well.</summary>
        public static List<ReportEntry> Run(Well well)
        {
            IConditioningStep[] steps =
            {
                new SkipRowsWithoutDepthStep(),   // 2
                new DepthToMetresStep(),          // 3
                new SortByDepthStep(),            // 4
                new RemoveDuplicateDepthsStep(),  // 5
                new SamplingCheckStep()           // 6
            };

            List<ReportEntry> entries = new List<ReportEntry>();
            foreach (IConditioningStep step in steps)
            {
                List<ReportEntry> stepEntries = step.Apply(well);
                entries.AddRange(stepEntries);
                if (HasFatal(stepEntries))
                {
                    break;
                }
            }
            return entries;
        }

        private static bool HasFatal(List<ReportEntry> entries)
        {
            foreach (ReportEntry entry in entries)
            {
                if (entry.Severity == Severity.Fatal)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
