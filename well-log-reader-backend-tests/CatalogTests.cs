using System.Collections.Generic;
using NUnit.Framework;
using well_log_reader_backend;

namespace well_log_reader_backend_tests
{
    [TestFixture]
    public class CatalogTests
    {
        [Test]
        public void Drho_IsNeverSelectedAsDensity()
        {
            Well well = MakeWell(Curve("DRHO", "G/CC"));

            List<ReportEntry> entries = new IdentifyCurvesStep().Apply(well);

            Assert.That(well.Curves[0].Family, Is.EqualTo(CurveFamily.None));
            Assert.That(entries.Exists(e => e.Message.Contains("No bulk density curve found")), Is.True);
        }

        [Test]
        public void UnitMismatch_IsNotSelectedAndWarns()
        {
            Well well = MakeWell(Curve("GR", "CPS"));

            List<ReportEntry> entries = new IdentifyCurvesStep().Apply(well);

            Assert.That(well.Curves[0].Family, Is.EqualTo(CurveFamily.None));
            ReportEntry warning = entries.Find(e => e.Message.Contains("not a gamma ray unit"));
            Assert.That(warning, Is.Not.Null);
            Assert.That(warning.Severity, Is.EqualTo(Severity.Warning));
        }

        [Test]
        public void PriorityOrder_IsCatalogOrderNotFileOrder()
        {
            // ECGR comes first in the file, but GR comes first in the catalog.
            Well well = MakeWell(Curve("ECGR", "GAPI"), Curve("GR", "GAPI"));

            List<ReportEntry> entries = new IdentifyCurvesStep().Apply(well);

            Assert.That(well.Curves[1].Family, Is.EqualTo(CurveFamily.GammaRay));
            Assert.That(well.Curves[0].Family, Is.EqualTo(CurveFamily.None));
            Assert.That(entries.Exists(e => e.Message == "GR selected as gamma ray (also found: ECGR)"), Is.True);
        }

        [Test]
        public void MnemonicMatch_IsCaseInsensitiveAndTrimmed()
        {
            Well well = MakeWell(Curve(" rhob ", "g/cc"));

            new IdentifyCurvesStep().Apply(well);

            Assert.That(well.Curves[0].Family, Is.EqualTo(CurveFamily.BulkDensity));
        }

        [Test]
        public void AllResistivityCurves_AreSelectedAndGrouped()
        {
            Well well = MakeWell(Curve("ILD", "OHMM"), Curve("AT90", "OHMM"), Curve("ILM", "OHMM"),
                                 Curve("SFLU", "OHMM"), Curve("MSFL", "OHMM"));

            new IdentifyCurvesStep().Apply(well);

            Assert.That(well.Curves[0].Family, Is.EqualTo(CurveFamily.ResistivityDeep));
            Assert.That(well.Curves[1].Family, Is.EqualTo(CurveFamily.ResistivityDeep));
            Assert.That(well.Curves[2].Family, Is.EqualTo(CurveFamily.ResistivityMedium));
            Assert.That(well.Curves[3].Family, Is.EqualTo(CurveFamily.ResistivityShallow));
            Assert.That(well.Curves[4].Family, Is.EqualTo(CurveFamily.ResistivityShallow));
        }

        [Test]
        public void Npss_IsReportedAsSandstoneMatrix()
        {
            Well well = MakeWell(Curve("NPSS", "V/V"));

            List<ReportEntry> entries = new IdentifyCurvesStep().Apply(well);

            Assert.That(well.Curves[0].Family, Is.EqualTo(CurveFamily.NeutronPorosity));
            Assert.That(entries.Exists(e => e.Message.Contains("sandstone")), Is.True);
        }

        [Test]
        public void KgPerM3_IsConvertedToGramsPerCc()
        {
            Well well = MakeWell(Curve("RHOB", "KG/M3", 2650f, float.NaN));
            new IdentifyCurvesStep().Apply(well);

            new ConvertCurveUnitsStep().Apply(well);

            Assert.That(well.Curves[0].Values[0], Is.EqualTo(2.65f).Within(1e-6));
            Assert.That(float.IsNaN(well.Curves[0].Values[1]), Is.True);
            Assert.That(well.Curves[0].Unit, Is.EqualTo("G/CC"));
            Assert.That(well.Curves[0].Info.unit, Is.EqualTo("KG/M3"), "the original unit is kept in Info");
        }

        [Test]
        public void Percent_IsConvertedToVv()
        {
            Well well = MakeWell(Curve("NPHI", "%", 25f));
            new IdentifyCurvesStep().Apply(well);

            new ConvertCurveUnitsStep().Apply(well);

            Assert.That(well.Curves[0].Values[0], Is.EqualTo(0.25f).Within(1e-6));
            Assert.That(well.Curves[0].Unit, Is.EqualTo("V/V"));
        }

        [Test]
        public void Cfcf_IsAcceptedAsVvWithoutConversion()
        {
            Well well = MakeWell(Curve("TNPH", "CFCF", 0.25f));
            new IdentifyCurvesStep().Apply(well);

            List<ReportEntry> entries = new ConvertCurveUnitsStep().Apply(well);

            Assert.That(well.Curves[0].Family, Is.EqualTo(CurveFamily.NeutronPorosity));
            Assert.That(well.Curves[0].Values[0], Is.EqualTo(0.25f));
            Assert.That(entries, Is.Empty);
        }

        [Test]
        public void NoUnit_IsSelectedButNotConvertedAndWarns()
        {
            Well well = MakeWell(Curve("NPHI", "", 25f));
            new IdentifyCurvesStep().Apply(well);

            List<ReportEntry> entries = new ConvertCurveUnitsStep().Apply(well);

            Assert.That(well.Curves[0].Family, Is.EqualTo(CurveFamily.NeutronPorosity));
            Assert.That(well.Curves[0].Values[0], Is.EqualTo(25f));
            Assert.That(entries[0].Message, Does.Contain("has no unit"));
        }

        [Test]
        public void GammaRayAndResistivity_KeepTheirUnits()
        {
            Well well = MakeWell(Curve("GR", "API", 80f), Curve("ILD", "OHM.M", 10f));
            new IdentifyCurvesStep().Apply(well);

            List<ReportEntry> entries = new ConvertCurveUnitsStep().Apply(well);

            Assert.That(well.Curves[0].Values[0], Is.EqualTo(80f));
            Assert.That(well.Curves[1].Values[0], Is.EqualTo(10f));
            Assert.That(entries, Is.Empty);
        }

        [Test]
        public void ImpossibleValues_BecomeNaN()
        {
            Well well = MakeWell(Curve("GR", "GAPI", -5f, 0f, 50f),
                                 Curve("ILD", "OHMM", 0f, -1f, 20f),
                                 Curve("RHOB", "G/CC", 0f, 2.3f, 2.4f));
            new IdentifyCurvesStep().Apply(well);

            List<ReportEntry> entries = new PhysicalCheckStep().Apply(well);

            Assert.That(well.Curves[0].Values, Is.EqualTo(new[] { float.NaN, 0f, 50f }), "GR 0 is possible, GR < 0 is not");
            Assert.That(well.Curves[1].Values, Is.EqualTo(new[] { float.NaN, float.NaN, 20f }));
            Assert.That(well.Curves[2].Values, Is.EqualTo(new[] { float.NaN, 2.3f, 2.4f }));
            Assert.That(entries.Exists(e => e.CurveMnemonic == "ILD" && e.Message.StartsWith("2 impossible")), Is.True);
        }

        [Test]
        public void UnusualValues_OnlyWarn()
        {
            Well well = MakeWell(Curve("GR", "GAPI", 600f, 50f),
                                 Curve("RHOB", "G/CC", 3.8f, 2.4f),
                                 Curve("NPHI", "V/V", -0.2f, 0.3f));
            new IdentifyCurvesStep().Apply(well);

            List<ReportEntry> entries = new PhysicalCheckStep().Apply(well);

            Assert.That(well.Curves[0].Values, Is.EqualTo(new[] { 600f, 50f }));
            Assert.That(well.Curves[1].Values, Is.EqualTo(new[] { 3.8f, 2.4f }));
            Assert.That(well.Curves[2].Values, Is.EqualTo(new[] { -0.2f, 0.3f }));
            Assert.That(entries.Count, Is.EqualTo(3));
            Assert.That(entries.TrueForAll(e => e.Severity == Severity.Warning), Is.True);
        }

        private static Curve Curve(string mnemonic, string unit, params float[] values)
        {
            Header info = new Header { mnem = mnemonic, unit = unit, line = 1 };
            return new Curve { Info = info, Unit = unit, Values = values };
        }

        private static Well MakeWell(params Curve[] curves)
        {
            Well well = new Well { FileName = "test.las" };
            well.CurveItems.Add(new Header { mnem = "DEPT", unit = "M" });
            foreach (Curve curve in curves)
            {
                well.CurveItems.Add(curve.Info);
                well.Curves.Add(curve);
            }
            return well;
        }
    }
}
