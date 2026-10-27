using System.Collections.Generic;
using System.Globalization;

namespace well_log_reader_backend
{
    /// <summary>
    /// Small text helpers shared by the header splitter and the data reader.
    /// </summary>
    public static class LasText
    {
        /// <summary>Tabs and other control characters count as spaces (DESIGN §5, reading details).</summary>
        public static bool IsSpace(char c)
        {
            return char.IsWhiteSpace(c) || char.IsControl(c);
        }

        /// <summary>Index of the first character that is not a space, or -1 for a blank line.</summary>
        public static int FirstNonSpace(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (!IsSpace(text[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Splits a data line into space-separated tokens. The list is cleared and reused
        /// by the caller, so the data loop does not allocate a new list for every line.
        /// </summary>
        public static void SplitTokens(string text, List<string> tokens)
        {
            tokens.Clear();
            int i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && IsSpace(text[i]))
                {
                    i++;
                }
                int start = i;
                while (i < text.Length && !IsSpace(text[i]))
                {
                    i++;
                }
                if (i > start)
                {
                    tokens.Add(text.Substring(start, i - start));
                }
            }
        }

        /// <summary>Parses a number the same way everywhere: invariant culture, no exceptions.</summary>
        public static bool TryParseNumber(string text, out double value)
        {
            if (text == null)
            {
                value = 0;
                return false;
            }
            return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Formats a number for report messages with invariant culture.</summary>
        public static string FormatNumber(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
