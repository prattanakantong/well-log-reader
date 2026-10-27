using System.Collections.Generic;
using NUnit.Framework;
using well_log_reader_backend;

namespace well_log_reader_backend_tests
{
    [TestFixture]
    public class ConditioningTests
    {
        [Test]
        public void Feet_AreConvertedToMetres_IncludingHeader()
        {
            Well well = MakeWell("FT", new double[] { 100, 101 }, new float[] { 1, 2 });
            well.WellItems.Add(new Header { mnem = "STRT", unit = "FT", value = "100" });
            well.WellItems.Add(new Header { mnem = "STEP", unit = "FT", value = "1" });

            List<ReportEntry> entries = new DepthToMetresStep().Apply(well);

            Assert.That(well.DepthUnit, Is.EqualTo("m"));
            Assert.That(well.Depth[0], Is.EqualTo(30.48).Within(1e-9));
            Assert.That(well.Depth[1], Is.EqualTo(30.7848).Within(1e-9));
            Assert.That(well.HeaderStart, Is.EqualTo(30.48).Within(1e-9));
            Assert.That(well.HeaderStep, Is.EqualTo(0.3048).Within(1e-9));
            Assert.That(entries[0].Severity, Is.EqualTo(Severity.Change));
        }

        [Test]
        public void Metres_AreNotChanged()
        {
            Well well = MakeWell("M", new double[] { 100, 101 }, new float[] { 1, 2 });

            List<ReportEntry> entries = new DepthToMetresStep().Apply(well);

            Assert.That(well.DepthUnit, Is.EqualTo("m"));
            Assert.That(well.Depth, Is.EqualTo(new double[] { 100, 101 }));
            Assert.That(entries, Is.Empty);
        }

        [Test]
        public void EmptyCurveUnit_UsesStrtUnit()
        {
            Well well = MakeWell("", new double[] { 10 }, new float[] { 1 });
            well.WellItems.Add(new Header { mnem = "STRT", unit = "F", value = "10" });

            new DepthToMetresStep().Apply(well);

            Assert.That(well.DepthUnit, Is.EqualTo("m"));
            Assert.That(well.Depth[0], Is.EqualTo(3.048).Within(1e-9));
        }

        [Test]
        public void UnknownDepthUnit_IsNotConvertedAndWarns()
        {
            Well well = MakeWell("", new double[] { 100, 101 }, new float[] { 1, 2 });

            List<ReportEntry> entries = new DepthToMetresStep().Apply(well);

            Assert.That(well.DepthUnit, Is.EqualTo(""));
            Assert.That(well.Depth, Is.EqualTo(new double[] { 100, 101 }));
            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].Severity, Is.EqualTo(Severity.Warning));
            Assert.That(entries[0].Message, Does.Contain("Depth unit unknown"));
        }

        [Test]
        public void DecreasingDepth_IsReversedWithCurves()
        {
            Well well = MakeWell("M", new double[] { 103, 102, 101 }, new float[] { 3, 2, 1 });

            List<ReportEntry> entries = new SortByDepthStep().Apply(well);

            Assert.That(well.Depth, Is.EqualTo(new double[] { 101, 102, 103 }));
            Assert.That(well.Curves[0].Values, Is.EqualTo(new float[] { 1, 2, 3 }));
            Assert.That(entries[0].Message, Does.Contain("decreasing"));
        }

        [Test]
        public void OutOfOrderDepth_IsSortedStably()
        {
            // Two rows at 101: the one first in the file (value 10) must stay first.
            Well well = MakeWell("M", new double[] { 102, 101, 100, 101 }, new float[] { 20, 10, 0, 11 });

            new SortByDepthStep().Apply(well);

            Assert.That(well.Depth, Is.EqualTo(new double[] { 100, 101, 101, 102 }));
            Assert.That(well.Curves[0].Values, Is.EqualTo(new float[] { 0, 10, 11, 20 }));
        }

        [Test]
        public void IncreasingDepth_IsNotReported()
        {
            Well well = MakeWell("M", new double[] { 100, 101 }, new float[] { 1, 2 });

            List<ReportEntry> entries = new SortByDepthStep().Apply(well);

            Assert.That(entries, Is.Empty);
        }

        [Test]
        public void DuplicateDepths_KeepFirstInFileOrder()
        {
            Well well = MakeWell("M", new double[] { 101, 100, 101, 102 }, new float[] { 10, 0, 11, 20 });

            new SortByDepthStep().Apply(well);
            List<ReportEntry> entries = new RemoveDuplicateDepthsStep().Apply(well);

            Assert.That(well.Depth, Is.EqualTo(new double[] { 100, 101, 102 }));
            Assert.That(well.Curves[0].Values, Is.EqualTo(new float[] { 0, 10, 20 }));
            Assert.That(entries[0].Message, Does.StartWith("1 row(s)"));
        }

        [Test]
        public void RowsWithoutDepth_AreSkipped()
        {
            Well well = MakeWell("M", new double[] { 100, double.NaN, 102 }, new float[] { 1, 2, 3 });

            List<ReportEntry> entries = new SkipRowsWithoutDepthStep().Apply(well);

            Assert.That(well.Depth, Is.EqualTo(new double[] { 100, 102 }));
            Assert.That(well.Curves[0].Values, Is.EqualTo(new float[] { 1, 3 }));
            Assert.That(entries[0].Message, Does.StartWith("1 row(s) without depth"));
        }

        [Test]
        public void AllRowsWithoutDepth_RejectsWell()
        {
            Well well = MakeWell("M", new double[] { double.NaN }, new float[] { 1 });

            List<ReportEntry> entries = new SkipRowsWithoutDepthStep().Apply(well);

            Assert.That(entries.Exists(e => e.Severity == Severity.Fatal), Is.True);
        }

        [Test]
        public void Sampling_ReportsStepMismatchAndGaps()
        {
            Well well = MakeWell("M", new double[] { 100, 100.5, 101, 103, 103.5 }, new float[] { 1, 2, 3, 4, 5 });
            well.WellItems.Add(new Header { mnem = "STRT", unit = "M", value = "100" });
            well.WellItems.Add(new Header { mnem = "STOP", unit = "M", value = "103.5" });
            well.WellItems.Add(new Header { mnem = "STEP", unit = "M", value = "0.25" });
            new DepthToMetresStep().Apply(well);

            List<ReportEntry> entries = new SamplingCheckStep().Apply(well);

            Assert.That(entries.Exists(e => e.Message.Contains("Header STEP is 0.25 m but the median step in the data is 0.5 m")), Is.True);
            Assert.That(entries.Exists(e => e.Message.Contains("Gap of 2 m between depth 101 and 103")), Is.True);
            Assert.That(entries.Exists(e => e.Message.Contains("STRT/STOP")), Is.False);
            Assert.That(well.Depth.Length, Is.EqualTo(5), "sampling check never changes the data");
        }

        [Test]
        public void Sampling_ReversedStrtStopIsAccepted()
        {
            Well well = MakeWell("M", new double[] { 100, 100.5, 101 }, new float[] { 1, 2, 3 });
            well.WellItems.Add(new Header { mnem = "STRT", unit = "M", value = "101" });
            well.WellItems.Add(new Header { mnem = "STOP", unit = "M", value = "100" });
            well.WellItems.Add(new Header { mnem = "STEP", unit = "M", value = "-0.5" });
            new DepthToMetresStep().Apply(well);

            List<ReportEntry> entries = new SamplingCheckStep().Apply(well);

            Assert.That(entries, Is.Empty);
        }

        [Test]
        public void Conditioner_RunsStepsInOrder()
        {
            Well well = MakeWell("FT", new double[] { 20, double.NaN, 10, 10 }, new float[] { 2, 9, 1, 5 });

            Conditioner.Run(well);

            Assert.That(well.Depth.Length, Is.EqualTo(2));
            Assert.That(well.Depth[0], Is.EqualTo(3.048).Within(1e-9));
            Assert.That(well.Depth[1], Is.EqualTo(6.096).Within(1e-9));
            Assert.That(well.Curves[0].Values, Is.EqualTo(new float[] { 1, 2 }));
        }

        /// <summary>A well with a DEPT index in the given unit and one GR curve.</summary>
        private static Well MakeWell(string depthUnit, double[] depth, float[] gr)
        {
            Well well = new Well { FileName = "test.las", Depth = depth };
            well.CurveItems.Add(new Header { mnem = "DEPT", unit = depthUnit });
            Header grInfo = new Header { mnem = "GR", unit = "GAPI" };
            well.CurveItems.Add(grInfo);
            well.Curves.Add(new Curve { Info = grInfo, Unit = "GAPI", Values = gr });
            return well;
        }
    }
}
