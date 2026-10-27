using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// What the LAS reader returns for one file. Problems are returned here, not thrown,
    /// so a bad file never crashes the program.
    /// </summary>
    public class LoadResult
    {
        public LoadResult(Well well, List<ReportEntry> entries)
        {
            Well = well;
            Entries = entries;
        }

        /// <summary>
        /// The loaded well. Never null: even a rejected file keeps its file name and
        /// whatever header was read, so the report can show the well name.
        /// </summary>
        public Well Well { get; }

        /// <summary>Everything the reader found, in file order.</summary>
        public List<ReportEntry> Entries { get; }

        /// <summary>True when the file was rejected (at least one Fatal entry, D12).</summary>
        public bool IsRejected
        {
            get
            {
                foreach (ReportEntry entry in Entries)
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
}
