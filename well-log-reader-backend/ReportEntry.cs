namespace well_log_reader_backend
{
    /// <summary>
    /// One warning, error or conditioning change. Every entry says where the problem is
    /// and why, so the file owner can act on it (D7).
    /// </summary>
    public class ReportEntry
    {
        /// <summary>Name of the LAS file, for example "WELL-A.las".</summary>
        public string FileName { get; set; } = "";

        /// <summary>1-based line number in the file; 0 if the entry is not about one line.</summary>
        public int LineNumber { get; set; }

        /// <summary>Section the line belongs to, for example "~W"; empty if not about a section.</summary>
        public string Section { get; set; } = "";

        /// <summary>Raw text of the line; only filled for line problems.</summary>
        public string RawLine { get; set; } = "";

        /// <summary>Mnemonic of the curve the entry is about; empty if none.</summary>
        public string CurveMnemonic { get; set; } = "";

        public Stage Stage { get; set; }

        public Severity Severity { get; set; }

        /// <summary>The reason, in plain English, for example "No dot or colon; line skipped".</summary>
        public string Message { get; set; } = "";
    }
}
