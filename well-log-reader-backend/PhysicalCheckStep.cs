using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// Step 9: checks the selected curves against physical limits (D16).
    /// Impossible values (for example density &lt;= 0) become NaN; unusual but possible values
    /// (for example density above 3.5 g/cc) only get a warning and are not changed.
    /// Runs after step 8, so the limits are compared in the standard unit.
    /// </summary>
    public class PhysicalCheckStep : IConditioningStep
    {
        public List<ReportEntry> Apply(Well well)
        {
            ReportLog log = new ReportLog(well.FileName, Stage.Condition);

            foreach (Curve curve in well.Curves)
            {
                CurveFamilyInfo info = CurveCatalog.Find(curve.Family);
                if (info == null)
                {
                    continue;
                }

                int impossible = 0;
                int unusual = 0;
                float[] values = curve.Values;
                for (int i = 0; i < values.Length; i++)
                {
                    float value = values[i];
                    if (float.IsNaN(value))
                    {
                        continue;
                    }
                    if (info.IsImpossible(value))
                    {
                        values[i] = float.NaN;
                        impossible++;
                    }
                    else if (value < info.UsualMin || value > info.UsualMax)
                    {
                        unusual++;
                    }
                }

                if (impossible > 0)
                {
                    log.Add(Severity.Change,
                        $"{impossible} impossible value(s) ({info.ImpossibleText}) set to NaN",
                        0, "", "", curve.Mnemonic);
                }
                if (unusual > 0)
                {
                    log.Add(Severity.Warning,
                        $"{unusual} unusual value(s) outside {RangeText(info)} {curve.Unit}; data unchanged",
                        0, "", "", curve.Mnemonic);
                }
            }
            return log.Entries;
        }

        private static string RangeText(CurveFamilyInfo info)
        {
            if (double.IsNegativeInfinity(info.UsualMin))
            {
                return "<= " + LasText.FormatNumber(info.UsualMax);
            }
            return LasText.FormatNumber(info.UsualMin) + " to " + LasText.FormatNumber(info.UsualMax);
        }
    }
}
