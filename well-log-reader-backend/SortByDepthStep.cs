using System;
using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// Step 4: re-orders rows to increasing depth (shallow to deep) with a stable sort (D14).
    /// Stable means rows with the same depth keep their file order, which step 5 relies on
    /// to keep the row that comes first in the file.
    /// </summary>
    public class SortByDepthStep : IConditioningStep
    {
        public List<ReportEntry> Apply(Well well)
        {
            ReportLog log = new ReportLog(well.FileName, Stage.Condition);
            double[] depth = well.Depth;

            int decreasingSteps = 0;
            int increasingSteps = 0;
            for (int i = 1; i < depth.Length; i++)
            {
                if (depth[i] < depth[i - 1])
                {
                    decreasingSteps++;
                }
                else if (depth[i] > depth[i - 1])
                {
                    increasingSteps++;
                }
            }

            if (decreasingSteps == 0)
            {
                return log.Entries; // already in order
            }

            int[] order = new int[depth.Length];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            // Array.Sort is not stable by itself; comparing the original index on equal depth makes it stable.
            Array.Sort(order, (a, b) =>
            {
                int byDepth = depth[a].CompareTo(depth[b]);
                return byDepth != 0 ? byDepth : a.CompareTo(b);
            });

            WellRows.Keep(well, order);

            if (increasingSteps == 0)
            {
                log.Add(Severity.Change, "Depth was decreasing; rows re-ordered to increasing depth");
            }
            else
            {
                log.Add(Severity.Change,
                    $"Depth was out of order ({decreasingSteps} step(s) going up); rows sorted to increasing depth");
            }
            return log.Entries;
        }
    }
}
