namespace well_log_reader_backend
{
    /// <summary>
    /// One parsed header line from the ~V, ~W, ~C or ~P section.
    /// All four sections share the same line format, so one type is used for all (D5).
    /// </summary>
    public class Header
    {
        /// <summary>Mnemonic: text before the first dot, trimmed.</summary>
        public string mnem { get; set; }

        /// <summary>Unit: text between the first dot and the first space after it. Empty if none.</summary>
        public string unit { get; set; }

        /// <summary>
        /// Value: text between the unit and the last colon, kept as raw text because
        /// header values can be numbers, names or dates. Convert only where needed.
        /// </summary>
        public string value { get; set; }

        /// <summary>Description: text after the last colon.</summary>
        public string description { get; set; }

        /// <summary>1-based line number in the source file, so problems can be reported (D7).</summary>
        public int line { get; set; }
    }
}
