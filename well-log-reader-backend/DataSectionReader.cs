using System;
using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// Reads the lines of the ~A section into one depth array and one value array per curve.
    /// Created by LasReader when ~A starts; fed one line at a time; Finish() checks the result.
    /// </summary>
    internal class DataSectionReader
    {
        /// <summary>Bad rows reported one by one; after that only a total is reported (D10).</summary>
        private const int MaxDetailedBadRows = 20;

        private readonly List<Header> curveItems;
        private readonly int columnCount;
        private readonly bool wrapped;
        private readonly double nullValue;
        private readonly ReportLog log;

        // Reused for every line so the data loop does not allocate a new list each time.
        private readonly List<string> tokens = new List<string>();

        // The growing data arrays. values[c] holds data column c + 1 (column 0 is depth).
        private double[] depth;
        private float[][] values;
        private int rowCount;

        // Unwrapped files: how many rows had each number of values, and the first bad rows.
        private readonly Dictionary<int, int> rowsByValueCount = new Dictionary<int, int>();
        private readonly List<BadRow> firstBadRows = new List<BadRow>();
        private int badRowCount;

        // Wrapped files: the values of the row being collected.
        private readonly double[] pendingRow;
        private int pendingCount;
        private int pendingFirstLine;

        // Per data column: NULL values and non-numeric tokens turned into NaN.
        private readonly int[] nullCounts;
        private readonly int[] nonNumericCounts;
        private readonly int[] firstNonNumericLine;
        private readonly string[] firstNonNumericText;

        public DataSectionReader(List<Header> curveItems, bool wrapped, double nullValue,
                                 int initialCapacity, ReportLog log)
        {
            this.curveItems = curveItems;
            this.columnCount = curveItems.Count;
            this.wrapped = wrapped;
            this.nullValue = nullValue;
            this.log = log;

            depth = new double[initialCapacity];
            values = new float[columnCount - 1][];
            for (int c = 0; c < values.Length; c++)
            {
                values[c] = new float[initialCapacity];
            }

            pendingRow = new double[columnCount];
            nullCounts = new int[columnCount];
            nonNumericCounts = new int[columnCount];
            firstNonNumericLine = new int[columnCount];
            firstNonNumericText = new string[columnCount];
        }

        /// <summary>Depth of every kept row, trimmed to the exact length by Finish().</summary>
        public double[] Depth
        {
            get { return depth; }
        }

        /// <summary>Values of data column c + 1, trimmed to the exact length by Finish().</summary>
        public float[][] Values
        {
            get { return values; }
        }

        /// <summary>Reads one non-blank, non-comment line of the ~A section.</summary>
        public void AddLine(string text, int lineNumber)
        {
            LasText.SplitTokens(text, tokens);

            if (wrapped)
            {
                AddWrappedTokens(lineNumber);
            }
            else
            {
                AddUnwrappedRow(text, lineNumber);
            }
        }

        /// <summary>
        /// Checks the data after the last line and reports what happened.
        /// Returns false when the file must be rejected (D9, D12).
        /// </summary>
        /// <param name="skippedCurveLinesHint">Text pointing to skipped ~C lines, added to a column-count rejection.</param>
        public bool Finish(string skippedCurveLinesHint)
        {
            if (!wrapped && !CheckColumnCount(skippedCurveLinesHint))
            {
                return false;
            }

            if (wrapped && pendingCount > 0)
            {
                log.Add(Severity.Warning,
                    $"{pendingCount} value(s) left over at the end of wrapped data (row starting at line {pendingFirstLine}); dropped",
                    pendingFirstLine, "~A");
            }

            if (rowCount == 0)
            {
                log.Add(Severity.Fatal, "No usable data row in ~A", 0, "~A");
                return false;
            }

            ReportBadRows();
            ReportValueCounts();

            Array.Resize(ref depth, rowCount);
            for (int c = 0; c < values.Length; c++)
            {
                Array.Resize(ref values[c], rowCount);
            }
            return true;
        }

        private void AddUnwrappedRow(string text, int lineNumber)
        {
            int count = tokens.Count;
            int seen;
            rowsByValueCount.TryGetValue(count, out seen);
            rowsByValueCount[count] = seen + 1;

            if (count != columnCount)
            {
                // Depth comes from the data (D1), so skipping this row does not shift other rows (D10).
                badRowCount++;
                if (firstBadRows.Count < MaxDetailedBadRows)
                {
                    firstBadRows.Add(new BadRow { LineNumber = lineNumber, Text = text, ValueCount = count });
                }
                return;
            }

            for (int c = 0; c < columnCount; c++)
            {
                pendingRow[c] = ParseValue(tokens[c], c, lineNumber);
            }
            AppendRow();
        }

        private void AddWrappedTokens(int lineNumber)
        {
            // Wrapped data is one stream of values cut into rows of columnCount values (D10).
            foreach (string token in tokens)
            {
                if (pendingCount == 0)
                {
                    pendingFirstLine = lineNumber;
                }
                pendingRow[pendingCount] = ParseValue(token, pendingCount, lineNumber);
                pendingCount++;

                if (pendingCount == columnCount)
                {
                    AppendRow();
                    pendingCount = 0;
                }
            }
        }

        /// <summary>
        /// Turns a token into a number. NULL values and non-numbers become NaN (D3, D10).
        /// NULL is compared as a parsed double, before any conversion to float (D3).
        /// </summary>
        private double ParseValue(string token, int column, int lineNumber)
        {
            double value;
            if (!LasText.TryParseNumber(token, out value))
            {
                nonNumericCounts[column]++;
                if (nonNumericCounts[column] == 1)
                {
                    firstNonNumericLine[column] = lineNumber;
                    firstNonNumericText[column] = token;
                }
                return double.NaN;
            }
            if (value == nullValue)
            {
                nullCounts[column]++;
                return double.NaN;
            }
            return value;
        }

        private void AppendRow()
        {
            if (rowCount == depth.Length)
            {
                Grow();
            }

            depth[rowCount] = pendingRow[0];
            for (int c = 0; c < values.Length; c++)
            {
                values[c][rowCount] = (float)pendingRow[c + 1];
            }
            rowCount++;
        }

        private void Grow()
        {
            int newSize = Math.Max(16, depth.Length * 2);
            Array.Resize(ref depth, newSize);
            for (int c = 0; c < values.Length; c++)
            {
                Array.Resize(ref values[c], newSize);
            }
        }

        /// <summary>
        /// The data has the number of columns that most rows have. If that differs from the
        /// number of ~C curves, the file is rejected (D9, D12); otherwise the odd rows are just bad rows (D10).
        /// </summary>
        private bool CheckColumnCount(string skippedCurveLinesHint)
        {
            int rowsWithCurveCount;
            rowsByValueCount.TryGetValue(columnCount, out rowsWithCurveCount);

            int mostCommonCount = columnCount;
            int mostCommonRows = rowsWithCurveCount;
            foreach (KeyValuePair<int, int> pair in rowsByValueCount)
            {
                if (pair.Value > mostCommonRows)
                {
                    mostCommonCount = pair.Key;
                    mostCommonRows = pair.Value;
                }
            }

            if (mostCommonCount == columnCount)
            {
                return true;
            }

            log.Add(Severity.Fatal,
                $"~C has {columnCount} curves but data has {mostCommonCount} columns" + skippedCurveLinesHint,
                0, "~A");
            return false;
        }

        private void ReportBadRows()
        {
            foreach (BadRow row in firstBadRows)
            {
                log.Add(Severity.Warning,
                    $"Row has {row.ValueCount} values but ~C defines {columnCount}; row skipped",
                    row.LineNumber, "~A", row.Text);
            }

            if (badRowCount > MaxDetailedBadRows)
            {
                log.Add(Severity.Warning,
                    $"{badRowCount} rows with the wrong number of values were skipped in total (first {MaxDetailedBadRows} listed)",
                    0, "~A");
            }
        }

        private void ReportValueCounts()
        {
            for (int c = 0; c < columnCount; c++)
            {
                string mnemonic = curveItems[c].mnem;

                if (nullCounts[c] > 0)
                {
                    log.Add(Severity.Change,
                        $"{nullCounts[c]} NULL value(s) ({LasText.FormatNumber(nullValue)}) set to NaN",
                        0, "~A", "", mnemonic);
                }

                if (nonNumericCounts[c] > 0)
                {
                    log.Add(Severity.Warning,
                        $"{nonNumericCounts[c]} value(s) are not numbers and were set to NaN (first: \"{firstNonNumericText[c]}\" at line {firstNonNumericLine[c]})",
                        firstNonNumericLine[c], "~A", "", mnemonic);
                }
            }
        }

        /// <summary>A data row with the wrong number of values, kept only for the detailed report.</summary>
        private class BadRow
        {
            public int LineNumber;
            public string Text;
            public int ValueCount;
        }
    }
}
