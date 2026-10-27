using System.IO;
using NUnit.Framework;
using well_log_reader_backend;

namespace well_log_reader_backend_tests
{
    [TestFixture]
    public class LasReaderTests
    {
        private const string Version20 =
            "~V\n" +
            " VERS.   2.0 : CWLS LOG ASCII STANDARD - VERSION 2.0\n" +
            " WRAP.   NO  : ONE LINE PER DEPTH STEP\n";

        private const string WellSection =
            "~W\n" +
            " STRT.M   100.0 : START DEPTH\n" +
            " STOP.M   100.5 : STOP DEPTH\n" +
            " STEP.M     0.5 : STEP\n" +
            " NULL.  -999.25 : NULL VALUE\n" +
            " WELL.  WELL-A  : WELL NAME\n";

        private const string CurveSection =
            "~C\n" +
            " DEPT.M    : DEPTH\n" +
            " GR  .GAPI : GAMMA RAY\n" +
            " RHOB.G/CC : BULK DENSITY\n";

        [Test]
        public void Unwrapped_ReadsDepthAndCurves()
        {
            LoadResult result = Read(Version20 + WellSection + CurveSection +
                "~A\n" +
                "100.0  50.0  2.30\n" +
                "100.5  60.0  2.40\n");

            Assert.That(result.IsRejected, Is.False);
            Well well = result.Well;
            Assert.That(well.WellName, Is.EqualTo("WELL-A"));
            Assert.That(well.Depth, Is.EqualTo(new double[] { 100.0, 100.5 }));
            Assert.That(well.Curves.Count, Is.EqualTo(2));
            Assert.That(well.Curves[0].Mnemonic, Is.EqualTo("GR"));
            Assert.That(well.Curves[0].Unit, Is.EqualTo("GAPI"));
            Assert.That(well.Curves[0].Values, Is.EqualTo(new float[] { 50f, 60f }));
            Assert.That(well.Curves[1].Values, Is.EqualTo(new float[] { 2.3f, 2.4f }));
            Assert.That(well.IsDepthIndexed, Is.True);
        }

        [Test]
        public void Wrapped_ReadsValuesAsOneStream()
        {
            string version =
                "~V\n" +
                " VERS.   2.0 : VERSION\n" +
                " WRAP.   YES : MULTIPLE LINES PER DEPTH STEP\n";

            LoadResult result = Read(version + WellSection + CurveSection +
                "~A\n" +
                "100.0\n" +
                "  50.0  2.30\n" +
                "100.5\n" +
                "  60.0  2.40\n");

            Assert.That(result.IsRejected, Is.False);
            Assert.That(result.Well.IsWrapped, Is.True);
            Assert.That(result.Well.Depth, Is.EqualTo(new double[] { 100.0, 100.5 }));
            Assert.That(result.Well.Curves[1].Values, Is.EqualTo(new float[] { 2.3f, 2.4f }));
        }

        [Test]
        public void Wrapped_LeftoverValuesAreDroppedAndReported()
        {
            string version =
                "~V\n" +
                " VERS.   2.0 : VERSION\n" +
                " WRAP.   YES : MULTIPLE LINES PER DEPTH STEP\n";

            LoadResult result = Read(version + WellSection + CurveSection +
                "~A\n" +
                "100.0\n" +
                "  50.0  2.30\n" +
                "100.5\n" +
                "  60.0\n");

            Assert.That(result.IsRejected, Is.False);
            Assert.That(result.Well.Depth, Is.EqualTo(new double[] { 100.0 }));
            AssertHasEntry(result, Severity.Warning, "left over");
        }

        [Test]
        public void RowWithWrongNumberOfValues_IsSkippedAndOtherDepthsUnchanged()
        {
            LoadResult result = Read(Version20 + WellSection + CurveSection +
                "~A\n" +
                "100.0  50.0  2.30\n" +
                "100.5  60.0\n" +
                "101.0  70.0  2.50  9.9\n" +
                "101.5  80.0  2.60\n");

            Assert.That(result.IsRejected, Is.False);
            Assert.That(result.Well.Depth, Is.EqualTo(new double[] { 100.0, 101.5 }));
            Assert.That(result.Well.Curves[0].Values, Is.EqualTo(new float[] { 50f, 80f }));

            ReportEntry tooFew = FindEntry(result, "Row has 2 values");
            Assert.That(tooFew.LineNumber, Is.EqualTo(16));
            Assert.That(tooFew.RawLine, Is.EqualTo("100.5  60.0"));
            Assert.That(FindEntry(result, "Row has 4 values"), Is.Not.Null);
        }

        [Test]
        public void NonNumericToken_BecomesNaNAndIsCounted()
        {
            LoadResult result = Read(Version20 + WellSection + CurveSection +
                "~A\n" +
                "100.0  abc  2.30\n" +
                "100.5  60.0  2.40\n");

            Assert.That(result.IsRejected, Is.False);
            Assert.That(float.IsNaN(result.Well.Curves[0].Values[0]), Is.True);
            Assert.That(result.Well.Curves[0].Values[1], Is.EqualTo(60f));

            ReportEntry entry = FindEntry(result, "not numbers");
            Assert.That(entry.CurveMnemonic, Is.EqualTo("GR"));
            Assert.That(entry.Severity, Is.EqualTo(Severity.Warning));
        }

        [Test]
        public void NullValue_BecomesNaNAndIsCounted()
        {
            LoadResult result = Read(Version20 + WellSection + CurveSection +
                "~A\n" +
                "100.0  -999.25  2.30\n" +
                "100.5  -999.2500  2.40\n");

            Assert.That(float.IsNaN(result.Well.Curves[0].Values[0]), Is.True);
            Assert.That(float.IsNaN(result.Well.Curves[0].Values[1]), Is.True);

            ReportEntry entry = FindEntry(result, "2 NULL value(s)");
            Assert.That(entry.Severity, Is.EqualTo(Severity.Change));
            Assert.That(entry.CurveMnemonic, Is.EqualTo("GR"));
        }

        [Test]
        public void MissingNull_UsesDefaultAndWarns()
        {
            string well =
                "~W\n" +
                " WELL.  WELL-A  : WELL NAME\n";

            LoadResult result = Read(Version20 + well + CurveSection +
                "~A\n" +
                "100.0  -999.25  2.30\n");

            Assert.That(float.IsNaN(result.Well.Curves[0].Values[0]), Is.True);
            AssertHasEntry(result, Severity.Warning, "No NULL line in ~W; using -999.25");
        }

        [Test]
        public void DuplicateWell_IsKeptAndReported()
        {
            string well = WellSection + " WELL.  WELL-B  : SECOND NAME\n";

            LoadResult result = Read(Version20 + well + CurveSection +
                "~A\n" +
                "100.0  50.0  2.30\n");

            Assert.That(result.Well.WellName, Is.EqualTo("WELL-A"));
            Assert.That(result.Well.WellItems.Count, Is.EqualTo(6));
            ReportEntry entry = FindEntry(result, "Duplicate mnemonic WELL");
            Assert.That(entry.LineNumber, Is.EqualTo(10));
            Assert.That(entry.Section, Is.EqualTo("~W"));
        }

        [Test]
        public void CurveCountDifferentFromDataColumns_IsRejectedWithCauseLine()
        {
            string curves =
                "~C\n" +
                " DEPT.M    : DEPTH\n" +
                " GR  .GAPI\n" +
                " RHOB.G/CC : BULK DENSITY\n";

            LoadResult result = Read(Version20 + WellSection + curves +
                "~A\n" +
                "100.0  50.0  2.30\n" +
                "100.5  60.0  2.40\n");

            Assert.That(result.IsRejected, Is.True);
            ReportEntry fatal = FindEntry(result, "~C has 2 curves but data has 3 columns");
            Assert.That(fatal.Severity, Is.EqualTo(Severity.Fatal));
            Assert.That(fatal.Message, Does.Contain("line 12"));

            ReportEntry cause = FindEntry(result, "No colon");
            Assert.That(cause.LineNumber, Is.EqualTo(12));
            Assert.That(cause.Section, Is.EqualTo("~C"));
            Assert.That(cause.RawLine, Is.EqualTo(" GR  .GAPI"));
        }

        [Test]
        public void WrappedWithSkippedCurveLine_IsRejected()
        {
            string version =
                "~V\n" +
                " VERS.   2.0 : VERSION\n" +
                " WRAP.   YES : MULTIPLE LINES PER DEPTH STEP\n";
            string curves =
                "~C\n" +
                " DEPT.M    : DEPTH\n" +
                " .DT57.US/F : MNEMONIC STARTS WITH A DOT\n" +
                " GR  .GAPI : GAMMA RAY\n";

            LoadResult result = Read(version + WellSection + curves +
                "~A\n" +
                "100.0\n" +
                "  120.0  50.0\n");

            Assert.That(result.IsRejected, Is.True);
            ReportEntry fatal = FindEntry(result, "Wrapped data cannot be checked against ~C");
            Assert.That(fatal.Severity, Is.EqualTo(Severity.Fatal));
            Assert.That(fatal.Message, Does.Contain("line 12"));
        }

        [Test]
        public void NoDataSection_IsRejected()
        {
            LoadResult result = Read(Version20 + WellSection + CurveSection);

            Assert.That(result.IsRejected, Is.True);
            AssertHasEntry(result, Severity.Fatal, "No ~A");
        }

        [Test]
        public void NoCurveSection_IsRejected()
        {
            LoadResult result = Read(Version20 + WellSection + "~A\n100.0 50.0\n");

            Assert.That(result.IsRejected, Is.True);
            AssertHasEntry(result, Severity.Fatal, "No ~C");
        }

        [Test]
        public void NoUsableRows_IsRejected()
        {
            LoadResult result = Read(Version20 + WellSection + CurveSection + "~A\n");

            Assert.That(result.IsRejected, Is.True);
            AssertHasEntry(result, Severity.Fatal, "No usable data row");
        }

        [Test]
        public void EmptyFile_IsRejected()
        {
            LoadResult result = Read("   \n\n");

            Assert.That(result.IsRejected, Is.True);
            AssertHasEntry(result, Severity.Fatal, "File is empty");
        }

        [Test]
        public void TimeIndex_IsLoadedButNotPlotted()
        {
            string curves =
                "~C\n" +
                " TIME.S    : ELAPSED TIME\n" +
                " GR  .GAPI : GAMMA RAY\n";

            LoadResult result = Read(Version20 + WellSection + curves +
                "~A\n" +
                "0.0  50.0\n" +
                "1.0  60.0\n");

            Assert.That(result.IsRejected, Is.False);
            Assert.That(result.Well.IsDepthIndexed, Is.False);
            AssertHasEntry(result, Severity.Warning, "not depth-indexed; log plot skipped");
        }

        [Test]
        public void MissingWrap_AssumesNoAndWarns()
        {
            string version = "~V\n VERS.   2.0 : VERSION\n";

            LoadResult result = Read(version + WellSection + CurveSection +
                "~A\n" +
                "100.0  50.0  2.30\n");

            Assert.That(result.Well.IsWrapped, Is.False);
            AssertHasEntry(result, Severity.Warning, "No WRAP line");
        }

        [Test]
        public void Las12_TakesWellValuesFromAfterTheColon()
        {
            string text =
                "~VERSION INFORMATION\n" +
                " VERS.                  1.2:   CWLS LOG ASCII STANDARD -VERSION 1.2\n" +
                " WRAP.                  NO:   ONE LINE PER DEPTH STEP\n" +
                "~WELL INFORMATION BLOCK\n" +
                " STRT.M        1670.000000:\n" +
                " STOP.M        1669.750000:\n" +
                " STEP.M            -0.1250:\n" +
                " NULL.           -999.2500:\n" +
                " WELL.                WELL:   ANY ET AL OIL WELL #12\n" +
                "~CURVE INFORMATION\n" +
                " DEPT.M                      :  1  DEPTH\n" +
                " ILD .OHMM                   :  8  DEEP RESISTIVITY\n" +
                "~A  DEPTH     ILD\n" +
                "1670.000   105.600\n" +
                "1669.875   -999.25\n";

            LoadResult result = Read(text);

            Assert.That(result.IsRejected, Is.False);
            Assert.That(result.Well.WellName, Is.EqualTo("ANY ET AL OIL WELL #12"));
            Assert.That(result.Well.StartItem.value, Is.EqualTo("1670.000000"));
            Assert.That(float.IsNaN(result.Well.Curves[0].Values[1]), Is.True);
        }

        [Test]
        public void CommentsAndBlankLines_AreIgnored()
        {
            LoadResult result = Read(Version20 + "# a comment\n\n" + WellSection + CurveSection +
                "~A\n" +
                "# comment in data\n" +
                "100.0  50.0  2.30\n" +
                "\n" +
                "100.5  60.0  2.40\n");

            Assert.That(result.IsRejected, Is.False);
            Assert.That(result.Well.Depth.Length, Is.EqualTo(2));
        }

        [Test]
        public void MissingFile_IsRejectedWithoutThrowing()
        {
            LoadResult result = LasReader.ReadFile(Path.Combine(Path.GetTempPath(), "does-not-exist-12345.las"));

            Assert.That(result.IsRejected, Is.True);
            AssertHasEntry(result, Severity.Fatal, "Cannot open file");
        }

        private static LoadResult Read(string text)
        {
            return LasReader.Read(new StringReader(text), "test.las");
        }

        private static ReportEntry FindEntry(LoadResult result, string messagePart)
        {
            foreach (ReportEntry entry in result.Entries)
            {
                if (entry.Message.Contains(messagePart))
                {
                    return entry;
                }
            }
            Assert.Fail("No entry containing \"" + messagePart + "\". Entries: " + Describe(result));
            return null;
        }

        private static void AssertHasEntry(LoadResult result, Severity severity, string messagePart)
        {
            ReportEntry entry = FindEntry(result, messagePart);
            Assert.That(entry.Severity, Is.EqualTo(severity));
        }

        private static string Describe(LoadResult result)
        {
            string text = "";
            foreach (ReportEntry entry in result.Entries)
            {
                text += "\n  [" + entry.Severity + "] line " + entry.LineNumber + ": " + entry.Message;
            }
            return text;
        }
    }
}
