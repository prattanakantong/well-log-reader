using System;
using System.IO;

namespace well_log_reader_backend
{
    /// <summary>
    /// Runs every stage for one file: load, condition, plot.
    /// One file at a time; only a small summary is returned and the well itself is released (D8).
    /// </summary>
    public static class WellProcessor
    {
        /// <summary>
        /// Processes one LAS file and writes its plot into plotsFolder. Never throws: an unexpected
        /// exception becomes a Fatal "internal error" entry, so the run can continue with the next file.
        /// </summary>
        public static WellSummary Process(string filePath, string plotsFolder)
        {
            WellSummary summary = new WellSummary { FileName = Path.GetFileName(filePath) };
            Stage stage = Stage.Load;

            try
            {
                LoadResult load = LasReader.ReadFile(filePath);
                Well well = load.Well;
                summary.Entries.AddRange(load.Entries);
                summary.WellName = well.WellName;

                if (!HasFatal(summary))
                {
                    stage = Stage.Condition;
                    summary.Entries.AddRange(Conditioner.Run(well));
                }

                summary.RowCount = well.Depth.Length;
                summary.CurveCount = well.Curves.Count;

                // A TIME-indexed well is loaded but not plotted (D12).
                if (!HasFatal(summary) && well.IsDepthIndexed)
                {
                    stage = Stage.Plot;
                    string plotFile = Path.GetFileNameWithoutExtension(filePath) + ".png"; // D18
                    LogPlotRenderer.Render(well, Path.Combine(plotsFolder, plotFile));
                    summary.PlotFile = plotFile;
                }
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

            summary.Status = DecideStatus(summary);
            return summary;
        }

        private static bool HasFatal(WellSummary summary)
        {
            foreach (ReportEntry entry in summary.Entries)
            {
                if (entry.Severity == Severity.Fatal)
                {
                    return true;
                }
            }
            return false;
        }

        private static WellStatus DecideStatus(WellSummary summary)
        {
            if (HasFatal(summary))
            {
                return WellStatus.Rejected;
            }
            if (summary.PlotFile == "")
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
