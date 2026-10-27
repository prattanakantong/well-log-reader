using System;
using System.Diagnostics;
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
            Stopwatch timer = Stopwatch.StartNew();

            try
            {
                LoadResult load = LasReader.ReadFile(filePath);
                Well well = load.Well;
                summary.Entries.AddRange(load.Entries);
                summary.WellName = well.WellName;
                summary.LoadTime = timer.Elapsed;

                if (!HasFatal(summary))
                {
                    stage = Stage.Condition;
                    timer.Restart();
                    summary.Entries.AddRange(Conditioner.Run(well));
                    summary.ConditionTime = timer.Elapsed;
                }

                summary.RowCount = well.Depth.Length;
                summary.CurveCount = well.Curves.Count;
                RecordSelectedCurves(well, summary);

                // A TIME-indexed well is loaded but not plotted (D12).
                if (!HasFatal(summary) && well.IsDepthIndexed)
                {
                    stage = Stage.Plot;
                    timer.Restart();
                    string plotFile = Path.GetFileNameWithoutExtension(filePath) + ".png"; // D18
                    LogPlotRenderer.Render(well, Path.Combine(plotsFolder, plotFile));
                    summary.PlotFile = plotFile;
                    summary.PlotTime = timer.Elapsed;
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

        /// <summary>Copies the mnemonics chosen by the catalog into the summary for the report table.</summary>
        private static void RecordSelectedCurves(Well well, WellSummary summary)
        {
            foreach (Curve curve in well.Curves)
            {
                switch (curve.Family)
                {
                    case CurveFamily.GammaRay:
                        summary.GammaRayCurve = curve.Mnemonic;
                        break;
                    case CurveFamily.ResistivityDeep:
                    case CurveFamily.ResistivityMedium:
                    case CurveFamily.ResistivityShallow:
                        summary.ResistivityCurves += (summary.ResistivityCurves.Length > 0 ? ", " : "") + curve.Mnemonic;
                        break;
                    case CurveFamily.BulkDensity:
                        summary.DensityCurve = curve.Mnemonic;
                        break;
                    case CurveFamily.NeutronPorosity:
                        summary.NeutronCurve = curve.Mnemonic;
                        break;
                }
            }
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
