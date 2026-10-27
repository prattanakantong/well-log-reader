namespace well_log_reader_backend
{
    /// <summary>Overall result of processing one file, shown in the progress line and the report.</summary>
    public enum WellStatus
    {
        /// <summary>Loaded and processed with no warnings.</summary>
        Ok,

        /// <summary>Loaded and processed, but the report has warnings.</summary>
        Warnings,

        /// <summary>Loaded, but no log plot was made (for example a TIME index, D12).</summary>
        NotPlotted,

        /// <summary>The file was rejected (D12) or failed with an internal error.</summary>
        Rejected
    }
}
