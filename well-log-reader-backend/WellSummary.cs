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
