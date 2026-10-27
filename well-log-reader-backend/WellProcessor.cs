using System;
using System.IO;

namespace well_log_reader_backend
{
    /// <summary>
    /// Runs every stage for one file: load, then (in later milestones) condition and plot.
    /// One file at a time; only a small summary is returned and the well itself is released (D8).
    /// </summary>
    public static class WellProcessor
    {
        /// <summary>
        /// Processes one LAS file. Never throws: an unexpected exception becomes a Fatal
        /// "internal error" entry, so the run can continue with the next file.
        /// </summary>
        public static WellSummary Process(string filePath)
        {
            WellSummary summary = new WellSummary { FileName = Path.GetFileName(filePath) };
            Stage stage = Stage.Load;
            Well well = null;

            try
            {
                LoadResult load = LasReader.ReadFile(filePath);
                well = load.Well;
                summary.Entries.AddRange(load.Entries);
                summary.WellName = well.WellName;
                summary.RowCount = well.Depth.Length;
                summary.CurveCount = well.Curves.Count;

                // Conditioning (M5, M6) and plotting (M7) will be added here,
                // each stage setting "stage" first so an internal error names the right stage.
            }
            catch (Exception ex)
            {
                // Last-resort catch: normal problems are report entries, so getting here means a bug.
                summary.Entries.Add(new ReportEntry
                {
                    FileName = summary.FileName,
                    Stage = stage,
                    Severity = Severity.Fatal,
                    Message = "internal error: " + ex.GetType().Name + ": " + ex.Message
                });
            }

            summary.Status = DecideStatus(summary, well);
            return summary;
        }

        private static WellStatus DecideStatus(WellSummary summary, Well well)
        {
            foreach (ReportEntry entry in summary.Entries)
            {
                if (entry.Severity == Severity.Fatal)
                {
                    return WellStatus.Rejected;
                }
            }
            if (well == null || !well.IsDepthIndexed)
            {
                return WellStatus.NotPlotted;
            }
            if (summary.WarningCount > 0)
            {
                return WellStatus.Warnings;
            }
            return WellStatus.Ok;
        }
    }
}
