using System;
using System.Collections.Generic;
using NUnit.Framework;
using well_log_reader_backend;

namespace well_log_reader_backend_tests
{
    [TestFixture]
    public class ReportWriterTests
    {
        [TestCase("plain", "plain")]
        [TestCase("a,b", "\"a,b\"")]
        [TestCase("say \"hi\"", "\"say \"\"hi\"\"\"")]
        [TestCase("two\nlines", "\"two\nlines\"")]
        [TestCase(" GR  .GAPI", "\" GR  .GAPI\"")]
        [TestCase("", "")]
        [TestCase("A\0B\u001aC", "A B C")]
        public void CsvField_FollowsRfc4180(string value, string expected)
        {
            Assert.That(ReportWriter.CsvField(value), Is.EqualTo(expected));
        }

        [Test]
        public void Csv_HasHeaderAndOneRowPerEntry()
        {
            List<WellSummary> summaries = new List<WellSummary> { MakeSummary() };

            string csv = ReportWriter.BuildCsv(summaries);
            string[] lines = csv.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            Assert.That(lines[0], Is.EqualTo("File,Well,Stage,Severity,Section,Line,Curve,Message,RawLine"));
            Assert.That(lines.Length, Is.EqualTo(4));
            Assert.That(lines[1], Is.EqualTo("A.las,\"WELL A, NORTH\",Condition,Change,,,GR,GR selected as gamma ray,"));
            Assert.That(lines[2], Is.EqualTo("A.las,\"WELL A, NORTH\",Load,Warning,~C,22,,No colon; line skipped,\" GR  .GAPI\""));
        }

        [Test]
        public void Markdown_ListsEntriesFatalFirstThenWarningThenChange()
        {
            List<WellSummary> summaries = new List<WellSummary> { MakeSummary() };
            RunStatistics stats = new RunStatistics(summaries, TimeSpan.FromSeconds(1.5));

            string md = ReportWriter.BuildMarkdown(summaries, stats, "in");

            int fatal = md.IndexOf("**Fatal**", StringComparison.Ordinal);
            int warning = md.IndexOf("**Warning**", StringComparison.Ordinal);
            int change = md.IndexOf("**Change**", StringComparison.Ordinal);
            Assert.That(fatal, Is.GreaterThan(0));
            Assert.That(fatal, Is.LessThan(warning));
            Assert.That(warning, Is.LessThan(change));
            Assert.That(md, Does.Contain("**Warning** (Load) line 22 (~C): No colon; line skipped `` GR  .GAPI ``"));
            Assert.That(md, Does.Contain("| Total time | 1.5 s |"));
        }

        [Test]
        public void Markdown_EscapesPipesInTableCells()
        {
            WellSummary summary = MakeSummary();
            summary.WellName = "A|B";

            string md = ReportWriter.BuildMarkdown(new List<WellSummary> { summary },
                new RunStatistics(new List<WellSummary> { summary }, TimeSpan.Zero), "in");

            Assert.That(md, Does.Contain("A\\|B"));
        }

        /// <summary>One file with a Change, a Warning and a Fatal entry, in that (unsorted) order.</summary>
        private static WellSummary MakeSummary()
        {
            WellSummary summary = new WellSummary
            {
                FileName = "A.las",
                WellName = "WELL A, NORTH",
                Status = WellStatus.Rejected,
                GammaRayCurve = "GR"
            };
            summary.Entries.Add(new ReportEntry
            {
                FileName = "A.las", Stage = Stage.Condition, Severity = Severity.Change,
                CurveMnemonic = "GR", Message = "GR selected as gamma ray"
            });
            summary.Entries.Add(new ReportEntry
            {
                FileName = "A.las", Stage = Stage.Load, Severity = Severity.Warning,
                LineNumber = 22, Section = "~C", RawLine = " GR  .GAPI", Message = "No colon; line skipped"
            });
            summary.Entries.Add(new ReportEntry
            {
                FileName = "A.las", Stage = Stage.Load, Severity = Severity.Fatal,
                Section = "~A", Message = "~C has 2 curves but data has 3 columns"
            });
            return summary;
        }
    }
}
