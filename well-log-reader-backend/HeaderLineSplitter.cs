namespace well_log_reader_backend
{
    /// <summary>
    /// Splits one header line of ~V, ~W, ~C or ~P into a Header (D5).
    /// LAS 2.0 rule: the first dot ends the mnemonic, the first space after the dot ends the unit,
    /// and the LAST colon separates the value from the description.
    /// The last colon is used because values such as times ("13:45:10") may contain colons.
    /// </summary>
    public static class HeaderLineSplitter
    {
        /// <summary>
        /// Tries to split a header line. Never throws; a line that cannot be split is not guessed (D6).
        /// </summary>
        /// <param name="text">The raw line text.</param>
        /// <param name="lineNumber">1-based line number, stored in the Header for reporting.</param>
        /// <param name="header">The split line, or null when the line cannot be split.</param>
        /// <param name="error">Why the line was skipped, or empty on success.</param>
        /// <returns>True when the line was split.</returns>
        public static bool TrySplit(string text, int lineNumber, out Header header, out string error)
        {
            header = null;
            error = "";

            if (text == null)
            {
                text = "";
            }

            int dot = text.IndexOf('.');
            int colon = text.LastIndexOf(':');

            if (dot < 0 && colon < 0)
            {
                error = "No dot or colon; line skipped";
                return false;
            }
            if (dot < 0)
            {
                error = "No dot; line skipped";
                return false;
            }
            if (colon < 0)
            {
                error = "No colon; line skipped";
                return false;
            }
            if (colon < dot)
            {
                // The only dot is in the description, so the line has no mnemonic part.
                error = "First dot comes after the last colon; line skipped";
                return false;
            }

            string mnemonic = text.Substring(0, dot).Trim();
            if (mnemonic.Length == 0)
            {
                error = "No mnemonic before the dot; line skipped";
                return false;
            }

            // The unit runs from just after the dot to the first space (or tab), but never past the colon.
            int unitEnd = dot + 1;
            while (unitEnd < colon && !IsSpace(text[unitEnd]))
            {
                unitEnd++;
            }

            header = new Header
            {
                mnem = mnemonic,
                unit = text.Substring(dot + 1, unitEnd - dot - 1).Trim(),
                value = text.Substring(unitEnd, colon - unitEnd).Trim(),
                description = text.Substring(colon + 1).Trim(),
                line = lineNumber
            };
            return true;
        }

        /// <summary>Tabs and other control characters count as spaces (DESIGN §5, reading details).</summary>
        private static bool IsSpace(char c)
        {
            return char.IsWhiteSpace(c) || char.IsControl(c);
        }
    }
}
