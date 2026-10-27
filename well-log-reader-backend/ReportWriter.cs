using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace well_log_reader_backend
{
    /// <summary>
    /// Writes the two reports of a run (D18): report.md for people and report.csv for filtering
    /// in Excel. The Build methods return the text, so the tests can check it without files.
    /// </summary>
    public static class ReportWriter
    {
        /// <summary>UTF-8 with a byte order mark, so Excel recognises the encoding of the CSV.</summary>
        private static readonly Encoding FileEncoding = new UTF8Encoding(true);

        public static void WriteMarkdown(string path, List<WellSummary> summaries, RunStatistics stats, string inputFolder)
        {
            File.WriteAllText(path, BuildMarkdown(summaries, stats, inputFolder), FileEncoding);
        }

        public static void WriteCsv(string path, List<WellSummary> summaries)
        {
            File.WriteAllText(path, BuildCsv(summaries), FileEncoding);
        }

        /// <summary>
        /// report.md: run summary, one table row per file, then one section per file with its
        /// entries, Fatal first, then Warning, then Change.
        /// </summary>
        public static string BuildMarkdown(List<WellSummary> summaries, RunStatistics stats, string inputFolder)
        {
            StringBuilder md = new StringBuilder();
            md.AppendLine("# Well Log Reader report");
            md.AppendLine();

            md.AppendLine("## Run summary");
            md.AppendLine();
            md.AppendLine("| Item | Value |");
            md.AppendLine("|---|---|");
            md.AppendLine("| Input folder | " + Cell(inputFolder) + " |");
            md.AppendLine("| Files | " + stats.FileCount + " |");
            md.AppendLine("| OK | " + stats.OkCount + " |");
            md.AppendLine("| OK with warnings | " + stats.WarningsCount + " |");
            md.AppendLine("| Not plotted | " + stats.NotPlottedCount + " |");
            md.AppendLine("| Rejected | " + stats.RejectedCount + " |");
            md.AppendLine("| Total time | " + Seconds(stats.TotalTime) + " |");
            md.AppendLine("| Load time (all files) | " + Seconds(stats.LoadTime) + " |");
            md.AppendLine("| Condition time (all files) | " + Seconds(stats.ConditionTime) + " |");
            md.AppendLine("| Plot time (all files) | " + Seconds(stats.PlotTime) + " |");
            md.AppendLine();

            md.AppendLine("## Files");
            md.AppendLine();
            md.AppendLine("| File | Well | Status | Rows | Curves | Gamma ray | Resistivity | Density | Neutron | Warnings | Plot |");
            md.AppendLine("|---|---|---|---:|---:|---|---|---|---|---:|---|");
            foreach (WellSummary s in summaries)
            {
                string plot = s.PlotFile == "" ? "-" : "[" + Cell(s.PlotFile) + "](<plots/" + s.PlotFile + ">)";
                md.AppendLine("| " + Cell(s.FileName) + " | " + Cell(s.WellName) + " | " + StatusText(s.Status) +
                              " | " + s.RowCount + " | " + s.CurveCount +
                              " | " + Dash(s.GammaRayCurve) + " | " + Dash(s.ResistivityCurves) +
                              " | " + Dash(s.DensityCurve) + " | " + Dash(s.NeutronCurve) +
                              " | " + s.WarningCount + " | " + plot + " |");
            }
            md.AppendLine();

            md.AppendLine("## Details per file");
            foreach (WellSummary s in summaries)
            {
                md.AppendLine();
                md.AppendLine("### " + s.FileName);
                md.AppendLine();
                if (s.Entries.Count == 0)
                {
                    md.AppendLine("No entries.");
                    continue;
                }

                // OrderBy is stable, so entries of the same severity keep the order they were found in.
                foreach (ReportEntry e in s.Entries.OrderBy(entry => entry.Severity))
                {
                    md.AppendLine("- " + EntryText(e));
                }
            }

            return md.ToString();
        }

        /// <summary>
        /// One entry as one line, for example:
        /// **Warning** (Load) line 22 (~C): No colon; line skipped `` GR  .GAPI ``
        /// </summary>
        private static string EntryText(ReportEntry e)
        {
            string text = "**" + e.Severity + "** (" + e.Stage + ")";

            string where = "";
            if (e.LineNumber > 0)
            {
                where = "line " + e.LineNumber;
            }
            if (e.Section != "")
            {
                where += (where == "" ? "" : " ") + "(" + e.Section + ")";
            }
            if (e.CurveMnemonic != "")
            {
                where += (where == "" ? "" : " ") + e.CurveMnemonic;
            }
            if (where != "")
            {
                text += " " + where;
            }

            text += ": " + Clean(e.Message);
            if (e.RawLine != "")
            {
                // A double-backtick code span shows the raw line exactly, even if it contains a backtick.
                text += " `` " + Clean(e.RawLine).Trim() + " ``";
            }
            return text;
        }

        /// <summary>
        /// report.csv: one row per entry with the columns
        /// File,Well,Stage,Severity,Section,Line,Curve,Message,RawLine (DESIGN §5).
        /// </summary>
        public static string BuildCsv(List<WellSummary> summaries)
        {
            StringBuilder csv = new StringBuilder();
            csv.Append("File,Well,Stage,Severity,Section,Line,Curve,Message,RawLine\r\n");

            foreach (WellSummary s in summaries)
            {
                foreach (ReportEntry e in s.Entries)
                {
                    string line = e.LineNumber > 0 ? e.LineNumber.ToString(CultureInfo.InvariantCulture) : "";
                    csv.Append(CsvField(e.FileName)).Append(',')
                       .Append(CsvField(s.WellName)).Append(',')
                       .Append(CsvField(e.Stage.ToString())).Append(',')
                       .Append(CsvField(e.Severity.ToString())).Append(',')
                       .Append(CsvField(e.Section)).Append(',')
                       .Append(line).Append(',')
                       .Append(CsvField(e.CurveMnemonic)).Append(',')
                       .Append(CsvField(e.Message)).Append(',')
                       .Append(CsvField(e.RawLine)).Append("\r\n");
                }
            }
            return csv.ToString();
        }

        /// <summary>
        /// RFC 4180 quoting: a field with a comma, a quote, a line break or leading/trailing spaces
        /// is put in quotes, and quotes inside it are doubled. Control characters other than line
        /// breaks are replaced by spaces first.
        /// </summary>
        public static string CsvField(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            value = Clean(value);

            bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ||
                               value[0] == ' ' || value[value.Length - 1] == ' ';
            if (!needsQuotes)
            {
                return value;
            }
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>Text for a Markdown table cell: a "|" would end the cell, so it is escaped.</summary>
        private static string Cell(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : Clean(value).Replace("|", "\\|");
        }

        /// <summary>
        /// Some LAS lines contain control characters such as NUL or Ctrl-Z. They count as spaces
        /// (DESIGN §5) and would make the reports look like binary files, so they are replaced.
        /// Line breaks are kept; CsvField quotes them.
        /// </summary>
        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            StringBuilder text = null;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsControl(c) && c != '\r' && c != '\n')
                {
                    if (text == null)
                    {
                        text = new StringBuilder(value);
                    }
                    text[i] = ' ';
                }
            }
            return text == null ? value : text.ToString();
        }

        private static string Dash(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : Cell(value);
        }

        private static string Seconds(TimeSpan time)
        {
            return time.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }

        /// <summary>Short status text, the same words as the console progress line.</summary>
        public static string StatusText(WellStatus status)
        {
            switch (status)
            {
                case WellStatus.Ok: return "OK";
                case WellStatus.Warnings: return "OK (warnings)";
                case WellStatus.NotPlotted: return "NOT PLOTTED";
                default: return "REJECTED";
            }
        }
    }
}
