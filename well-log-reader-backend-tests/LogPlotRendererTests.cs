using System.IO;
using NUnit.Framework;
using SkiaSharp;
using well_log_reader_backend;

namespace well_log_reader_backend_tests
{
    /// <summary>
    /// Smoke tests: the renderer must produce a PNG of the fixed size for normal, empty and
    /// gappy wells. Whether the plot looks right is checked by eye on the real files (M7).
    /// </summary>
    [TestFixture]
    public class LogPlotRendererTests
    {
        private string folder;

        [SetUp]
        public void CreateFolder()
        {
            folder = Path.Combine(Path.GetTempPath(), "well-log-reader-tests-" + Path.GetRandomFileName());
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void DeleteFolder()
        {
            Directory.Delete(folder, true);
        }

        [Test]
        public void WellWithAllFamiliesAndGaps_IsRenderedAtFixedSize()
        {
            Well well = MakeWell(new double[] { 100, 101, 102, 103 });
            AddCurve(well, "GR", "GAPI", CurveFamily.GammaRay, 50f, float.NaN, 70f, 80f);
            AddCurve(well, "ILD", "OHMM", CurveFamily.ResistivityDeep, 10f, 20f, float.NaN, 5000f);
            AddCurve(well, "ILM", "OHMM", CurveFamily.ResistivityMedium, 8f, 9f, 10f, 11f);
            AddCurve(well, "RHOB", "G/CC", CurveFamily.BulkDensity, 2.3f, 2.4f, 2.5f, 2.6f);
            AddCurve(well, "NPHI", "V/V", CurveFamily.NeutronPorosity, 0.3f, 0.2f, 0.1f, 0.0f);

            string path = Render(well);

            AssertImageSize(path);
        }

        [Test]
        public void WellWithoutTargetCurves_IsRenderedWithNoDataTracks()
        {
            Well well = MakeWell(new double[] { 100, 101 });
            AddCurve(well, "CALI", "IN", CurveFamily.None, 8f, 8.5f);

            string path = Render(well);

            AssertImageSize(path);
        }

        [Test]
        public void SingleRowWell_IsRendered()
        {
            Well well = MakeWell(new double[] { 100 });
            AddCurve(well, "GR", "GAPI", CurveFamily.GammaRay, 50f);

            string path = Render(well);

            AssertImageSize(path);
        }

        private string Render(Well well)
        {
            string path = Path.Combine(folder, "test.png");
            LogPlotRenderer.Render(well, path);
            return path;
        }

        private static void AssertImageSize(string path)
        {
            Assert.That(File.Exists(path), Is.True);
            using (SKBitmap bitmap = SKBitmap.Decode(path))
            {
                Assert.That(bitmap.Width, Is.EqualTo(PlotSettings.ImageWidth));
                Assert.That(bitmap.Height, Is.EqualTo(PlotSettings.ImageHeight));
            }
        }

        private static Well MakeWell(double[] depth)
        {
            Well well = new Well { FileName = "test.las", Depth = depth, DepthUnit = "m" };
            well.WellItems.Add(new Header { mnem = "WELL", value = "TEST WELL" });
            return well;
        }

        private static void AddCurve(Well well, string mnemonic, string unit, CurveFamily family, params float[] values)
        {
            Header info = new Header { mnem = mnemonic, unit = unit };
            well.CurveItems.Add(info);
            well.Curves.Add(new Curve { Info = info, Unit = unit, Family = family, Values = values });
        }
    }
}
