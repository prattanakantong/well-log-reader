using System;
using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// Counts and timings of a whole run, computed from the well summaries.
    /// Used by the console summary and by report.md, so both always show the same numbers.
    /// </summary>
    public class RunStatistics
    {
        public RunStatistics(List<WellSummary> summaries, TimeSpan totalTime)
        {
            TotalTime = totalTime;
            FileCount = summaries.Count;

            foreach (WellSummary summary in summaries)
            {
                switch (summary.Status)
                {
                    case WellStatus.Ok: OkCount++; break;
                    case WellStatus.Warnings: WarningsCount++; break;
                    case WellStatus.NotPlotted: NotPlottedCount++; break;
                    default: RejectedCount++; break;
                }

                LoadTime += summary.LoadTime;
                ConditionTime += summary.ConditionTime;
                PlotTime += summary.PlotTime;
            }
        }

        public int FileCount { get; }
        public int OkCount { get; }
        public int WarningsCount { get; }
        public int NotPlottedCount { get; }
        public int RejectedCount { get; }

        /// <summary>Wall-clock time of the whole run.</summary>
        public TimeSpan TotalTime { get; }

        // Sum over all files of the time spent in each stage.
        public TimeSpan LoadTime { get; }
        public TimeSpan ConditionTime { get; }
        public TimeSpan PlotTime { get; }
    }
}
