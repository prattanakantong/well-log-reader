using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// Step 5: keeps only the first row (in file order) of each repeated depth (D14).
    /// Runs after the stable sort, so equal depths are next to each other and the
    /// first one is the one that came first in the file.
    /// </summary>
    public class RemoveDuplicateDepthsStep : IConditioningStep
    {
        public List<ReportEntry> Apply(Well well)
        {
            ReportLog log = new ReportLog(well.FileName, Stage.Condition);
            double[] depth = well.Depth;

            List<int> keep = new List<int>(depth.Length);
            for (int i = 0; i < depth.Length; i++)
            {
                // Exact comparison is correct here: equal depths come from the same text in the
                // file and went through the same conversion, so they are bit-for-bit equal.
                if (i == 0 || depth[i] != depth[i - 1])
                {
                    keep.Add(i);
                }
            }

            int removed = depth.Length - keep.Count;
            if (removed > 0)
            {
                WellRows.Keep(well, keep.ToArray());
                log.Add(Severity.Change, $"{removed} row(s) with a repeated depth removed; the first in file order was kept");
            }
            return log.Entries;
        }
    }
}
