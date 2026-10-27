namespace well_log_reader_backend
{
    /// <summary>
    /// How serious a report entry is (D7).
    /// The order is the order used in report.md: Fatal first, then Warning, then Change.
    /// </summary>
    public enum Severity
    {
        /// <summary>The file was rejected (D12).</summary>
        Fatal,

        /// <summary>Something is wrong or suspicious, but the file is still used.</summary>
        Warning,

        /// <summary>Conditioning changed the data (for example a unit conversion).</summary>
        Change
    }
}
