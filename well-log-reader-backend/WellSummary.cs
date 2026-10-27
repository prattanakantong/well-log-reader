using System;
using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// The small part of a well that is kept until the end of the run to build the final report.
    /// The large depth and curve arrays are not kept, so memory does not grow with the number of wells (D8).
    /// </summary>
    public class WellSummary
    {
        /// <summary>Name of the LAS file, for example "WELL-A.las".</summary>
        public string FileName { get; set; } = "";

        /// <summary>Well name from the WELL line, or empty if unknown.</summary>
        public string WellName { get; set; } = "";

        public WellStatus Status { get; set; }

        /// <summary>Number of data rows loaded.</summary>
        public int RowCount { get; set; }

        /// <summary>Number of curves, not counting the depth column.</summary>
        public int CurveCount { get; set; }

        /// <summary>File name of the PNG plot, for example "WELL-A.png"; empty when no plot was made.</summary>
        public string PlotFile { get; set; } = "";

        // Curves chosen by the catalog (D15), for the report table. Empty when none was found.

        /// <summary>Mnemonic of the gamma ray curve.</summary>
        public string GammaRayCurve { get; set; } = "";

        /// <summary>Mnemonics of all resistivity curves, comma-separated.</summary>
        public string ResistivityCurves { get; set; } = "";

        /// <summary>Mnemonic of the bulk density curve.</summary>
        public string DensityCurve { get; set; } = "";

        /// <summary>Mnemonic of the neutron porosity curve.</summary>
        public string NeutronCurve { get; set; } = "";

        // Time spent in each stage, measured with Stopwatch, for the run summary.

        public TimeSpan LoadTime { get; set; }

        public TimeSpan ConditionTime { get; set; }

        public TimeSpan PlotTime { get; set; }

        /// <summary>All report entries of this file, from every stage.</summary>
        public List<ReportEntry> Entries { get; } = new List<ReportEntry>();

        /// <summary>Number of Warning entries.</summary>
        public int WarningCount
        {
            get { return CountEntries(Severity.Warning); }
        }

        private int CountEntries(Severity severity)
        {
            int count = 0;
            foreach (ReportEntry entry in Entries)
            {
                if (entry.Severity == severity)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
