using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// Collects the report entries of one file for one stage, so callers do not have to
    /// repeat the file name and stage on every entry (D7).
    /// </summary>
    public class ReportLog
    {
        private readonly string fileName;
        private readonly Stage stage;

        public ReportLog(string fileName, Stage stage)
        {
            this.fileName = fileName;
            this.stage = stage;
        }

        /// <summary>All entries added so far, in the order they were found.</summary>
        public List<ReportEntry> Entries { get; } = new List<ReportEntry>();

        /// <summary>True when at least one Fatal entry was added (the file is rejected).</summary>
        public bool HasFatal { get; private set; }

        /// <summary>Adds one entry. Line, section, raw text and curve are optional.</summary>
        public void Add(Severity severity, string message, int lineNumber = 0,
                        string section = "", string rawLine = "", string curveMnemonic = "")
        {
            Entries.Add(new ReportEntry
            {
                FileName = fileName,
                LineNumber = lineNumber,
                Section = section,
                RawLine = rawLine,
                CurveMnemonic = curveMnemonic,
                Stage = stage,
                Severity = severity,
                Message = message
            });

            if (severity == Severity.Fatal)
            {
                HasFatal = true;
            }
        }
    }
}
