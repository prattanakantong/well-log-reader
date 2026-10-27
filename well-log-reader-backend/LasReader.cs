using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;

namespace well_log_reader_backend
{
    /// <summary>
    /// Loads one LAS file into a Well. Normal problems become report entries, never exceptions,
    /// so a bad file never crashes the program.
    /// Use ReadFile for a file on disk, or Read for any TextReader (used by the tests).
    /// </summary>
    public class LasReader
    {
        /// <summary>Used when the file has no usable NULL line (D11).</summary>
        public const double DefaultNullValue = -999.25;

        /// <summary>Upper limit for the initial array size estimated from STRT/STOP/STEP.</summary>
        private const int MaxInitialCapacity = 1000000;
        private const int DefaultInitialCapacity = 1024;

        private readonly Well well;
        private readonly ReportLog log;

        private int lineNumber;
        private bool hasContent;
        private char section;            // current section letter, '\0' before the first section
        private string sectionName = ""; // for example "~W", used in report entries
        private bool sawCurveSection;
        private bool sawDataSection;
        private bool isLas12;
        private double nullValue = DefaultNullValue;
        private DataSectionReader data;

        // First line of each mnemonic per section, to report duplicates (D5).
        private readonly Dictionary<string, int> firstLineOfMnemonic =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // ~C lines that could not be split; quoted when the column count does not match (D9).
        private readonly List<int> skippedCurveLines = new List<int>();

        private readonly StringBuilder otherText = new StringBuilder();
        private readonly StringBuilder unknownSectionsText = new StringBuilder();

        private LasReader(Well well, ReportLog log)
        {
            this.well = well;
            this.log = log;
        }

        /// <summary>Opens and reads one LAS file from disk.</summary>
        public static LoadResult ReadFile(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            Well well = new Well { FilePath = filePath, FileName = fileName };
            ReportLog log = new ReportLog(fileName, Stage.Load);

            StreamReader reader;
            try
            {
                // UTF-8 with BOM detection; invalid bytes become the replacement character instead of throwing.
                reader = new StreamReader(filePath, new UTF8Encoding(false, false), true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is ArgumentException || ex is NotSupportedException ||
                                       ex is SecurityException)
            {
                log.Add(Severity.Fatal, "Cannot open file: " + ex.Message);
                return new LoadResult(well, log.Entries);
            }

            using (reader)
            {
                return new LasReader(well, log).Run(reader);
            }
        }

        /// <summary>Reads LAS text from any reader. filePath is only used for names in the result.</summary>
        public static LoadResult Read(TextReader reader, string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            Well well = new Well { FilePath = filePath, FileName = fileName };
            ReportLog log = new ReportLog(fileName, Stage.Load);
            return new LasReader(well, log).Run(reader);
        }

        private LoadResult Run(TextReader reader)
        {
            string text;
            while ((text = reader.ReadLine()) != null)
            {
                lineNumber++;

                int first = LasText.FirstNonSpace(text);
                if (first < 0)
                {
                    continue; // blank line
                }
                hasContent = true;

                char c = text[first];
                if (c == '#')
                {
                    continue; // comment line
                }

                if (c == '~')
                {
                    if (section == 'A')
                    {
                        // ~A must be the last section; anything after it cannot be trusted as data.
                        log.Add(Severity.Warning, "Section found after ~A; rest of the file ignored",
                            lineNumber, "~A", text);
                        break;
                    }
                    if (!StartSection(text, first))
                    {
                        break; // the file was rejected when ~A started
                    }
                    continue;
                }

                HandleLine(text);
            }

            return Finish();
        }

        /// <summary>Handles a "~X" line. Returns false when reading must stop.</summary>
        private bool StartSection(string text, int tildeIndex)
        {
            section = tildeIndex + 1 < text.Length ? char.ToUpperInvariant(text[tildeIndex + 1]) : '?';
            sectionName = "~" + section;

            switch (section)
            {
                case 'C':
                    sawCurveSection = true;
                    return true;
                case 'A':
                    sawDataSection = true;
                    return BeginData();
                case 'V':
                case 'W':
                case 'P':
                case 'O':
                    return true;
                default:
                    unknownSectionsText.AppendLine(text);
                    return true;
            }
        }

        private void HandleLine(string text)
        {
            switch (section)
            {
                case 'V':
                    AddHeader(text, well.VersionItems);
                    break;
                case 'W':
                    AddHeader(text, well.WellItems);
                    break;
                case 'C':
                    AddHeader(text, well.CurveItems);
                    break;
                case 'P':
                    AddHeader(text, well.ParameterItems);
                    break;
                case 'O':
                    otherText.AppendLine(text);
                    break;
                case 'A':
                    data.AddLine(text, lineNumber);
                    break;
                case '\0':
                    log.Add(Severity.Warning, "Line before the first section; ignored", lineNumber, "", text);
                    break;
                default:
                    unknownSectionsText.AppendLine(text);
                    break;
            }
        }

        /// <summary>Splits a header line (D5); a line that cannot be split is skipped and reported (D6, D9).</summary>
        private void AddHeader(string text, List<Header> items)
        {
            Header header;
            string error;
            if (!HeaderLineSplitter.TrySplit(text, lineNumber, out header, out error))
            {
                log.Add(Severity.Warning, error, lineNumber, sectionName, text);
                if (section == 'C')
                {
                    skippedCurveLines.Add(lineNumber);
                }
                return;
            }

            // Duplicates are kept in file order and reported (D5).
            string key = sectionName + " " + header.mnem;
            int firstLine;
            if (firstLineOfMnemonic.TryGetValue(key, out firstLine))
            {
                string effect = section == 'C' ? "both columns are kept" : "the first one is used";
                log.Add(Severity.Warning,
                    $"Duplicate mnemonic {header.mnem} (first at line {firstLine}); {effect}",
                    lineNumber, sectionName, text, section == 'C' ? header.mnem : "");
            }
            else
            {
                firstLineOfMnemonic[key] = lineNumber;
            }

            items.Add(header);
        }

        /// <summary>
        /// Called when ~A starts: the header is complete, so read VERS, WRAP and NULL
        /// and prepare the data arrays. Returns false when the file is rejected.
        /// </summary>
        private bool BeginData()
        {
            if (!sawCurveSection)
            {
                log.Add(Severity.Fatal, "No ~C (curve) section before ~A", lineNumber, "~A");
                return false;
            }
            if (well.CurveItems.Count == 0)
            {
                log.Add(Severity.Fatal, "~C section has no usable curve line", lineNumber, "~A");
                return false;
            }

            ReadVersionSection();
            ReadWellSection();

            data = new DataSectionReader(well.CurveItems, well.IsWrapped, nullValue, EstimateRowCount(), log);
            return true;
        }

        private void ReadVersionSection()
        {
            Header vers = well.FindVersionItem("VERS");
            if (vers == null)
            {
                log.Add(Severity.Warning, "No VERS line in ~V; read with LAS 2.0 rules", 0, "~V");
            }
            else
            {
                well.Version = vers.value;
                if (vers.value.StartsWith("3.", StringComparison.Ordinal))
                {
                    log.Add(Severity.Warning, "LAS 3.0 is not supported; read with LAS 2.0 rules",
                        vers.line, "~V");
                }
                isLas12 = vers.value.StartsWith("1.", StringComparison.Ordinal);
            }

            Header wrap = well.FindVersionItem("WRAP");
            if (wrap == null)
            {
                log.Add(Severity.Warning, "No WRAP line in ~V; assuming NO", 0, "~V");
            }
            else if (string.Equals(wrap.value, "YES", StringComparison.OrdinalIgnoreCase))
            {
                well.IsWrapped = true;
            }
            else if (!string.Equals(wrap.value, "NO", StringComparison.OrdinalIgnoreCase))
            {
                log.Add(Severity.Warning, $"WRAP value \"{wrap.value}\" is not YES or NO; assuming NO",
                    wrap.line, "~V");
            }
        }

        private void ReadWellSection()
        {
            if (isLas12)
            {
                UseLas12WellValues();
            }

            Header nullItem = well.NullItem;
            double parsed;
            if (nullItem == null)
            {
                log.Add(Severity.Warning,
                    $"No NULL line in ~W; using {LasText.FormatNumber(DefaultNullValue)}", 0, "~W");
            }
            else if (!LasText.TryParseNumber(nullItem.value, out parsed))
            {
                log.Add(Severity.Warning,
                    $"NULL value \"{nullItem.value}\" is not a number; using {LasText.FormatNumber(DefaultNullValue)}",
                    nullItem.line, "~W");
            }
            else
            {
                nullValue = parsed;
            }
        }

        /// <summary>
        /// LAS 1.2 puts most ~W values after the colon; the text before it is only a label,
        /// for example "WELL.  WELL:  ANY ET AL OIL WELL #12" (D5).
        /// </summary>
        private void UseLas12WellValues()
        {
            foreach (Header item in well.WellItems)
            {
                string mnem = item.mnem.ToUpperInvariant();
                if (mnem != "STRT" && mnem != "STOP" && mnem != "STEP" && mnem != "NULL")
                {
                    item.value = item.description;
                }
            }
            log.Add(Severity.Change, "LAS 1.2 file: ~W values taken from the text after the colon", 0, "~W");
        }

        /// <summary>
        /// STRT/STOP/STEP are used only to guess the array size, never for depth (D1).
        /// </summary>
        private int EstimateRowCount()
        {
            double start, stop, step;
            if (well.StartItem == null || well.StopItem == null || well.StepItem == null ||
                !LasText.TryParseNumber(well.StartItem.value, out start) ||
                !LasText.TryParseNumber(well.StopItem.value, out stop) ||
                !LasText.TryParseNumber(well.StepItem.value, out step) ||
                step == 0 || double.IsNaN(start) || double.IsNaN(stop) || double.IsNaN(step))
            {
                return DefaultInitialCapacity;
            }

            double rows = Math.Abs((stop - start) / step) + 1;
            if (double.IsInfinity(rows) || rows > MaxInitialCapacity)
            {
                return MaxInitialCapacity;
            }
            return Math.Max(1, (int)rows);
        }

        private LoadResult Finish()
        {
            well.OtherText = otherText.ToString();
            well.UnknownSectionsText = unknownSectionsText.ToString();

            if (!hasContent)
            {
                log.Add(Severity.Fatal, "File is empty");
                return new LoadResult(well, log.Entries);
            }
            if (log.HasFatal)
            {
                return new LoadResult(well, log.Entries);
            }
            if (!sawCurveSection)
            {
                log.Add(Severity.Fatal, "No ~C (curve) section");
            }
            if (!sawDataSection)
            {
                log.Add(Severity.Fatal, "No ~A (data) section");
            }
            if (log.HasFatal)
            {
                return new LoadResult(well, log.Entries);
            }

            if (!data.Finish(SkippedCurveLinesHint()))
            {
                return new LoadResult(well, log.Entries);
            }

            well.Depth = data.Depth;
            for (int c = 1; c < well.CurveItems.Count; c++)
            {
                Header info = well.CurveItems[c];
                well.Curves.Add(new Curve { Info = info, Unit = info.unit, Values = data.Values[c - 1] });
            }

            Header indexCurve = well.CurveItems[0];
            well.IsDepthIndexed = DepthIndex.LooksLikeDepth(indexCurve);
            if (!well.IsDepthIndexed)
            {
                log.Add(Severity.Warning,
                    $"First column {indexCurve.mnem} is not depth-indexed; log plot skipped",
                    indexCurve.line, "~C", "", indexCurve.mnem);
            }

            return new LoadResult(well, log.Entries);
        }

        /// <summary>Points the column-count rejection at the ~C lines that were skipped (D9).</summary>
        private string SkippedCurveLinesHint()
        {
            if (skippedCurveLines.Count == 0)
            {
                return "";
            }
            return $" ({skippedCurveLines.Count} ~C line(s) skipped, see line {string.Join(", ", skippedCurveLines)})";
        }
    }
}
