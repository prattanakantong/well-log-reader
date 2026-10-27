using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// Step 2: drops rows whose depth is missing (NULL or not a number, already NaN).
    /// A row without depth has no position, so it cannot be plotted or sorted (D14).
    /// </summary>
    public class SkipRowsWithoutDepthStep : IConditioningStep
    {
        public List<ReportEntry> Apply(Well well)
        {
            ReportLog log = new ReportLog(well.FileName, Stage.Condition);

            List<int> keep = new List<int>(well.Depth.Length);
            for (int i = 0; i < well.Depth.Length; i++)
            {
                if (!double.IsNaN(well.Depth[i]))
                {
                    keep.Add(i);
                }
            }

            int skipped = well.Depth.Length - keep.Count;
            if (skipped == 0)
            {
                return log.Entries;
            }

            WellRows.Keep(well, keep.ToArray());
            log.Add(Severity.Change, $"{skipped} row(s) without depth skipped", 0, "~A");

            if (keep.Count == 0)
            {
                log.Add(Severity.Fatal, "No data row with a depth value", 0, "~A");
            }
            return log.Entries;
        }
    }
}
