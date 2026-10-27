using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// Step 8: converts the selected curves to the family's standard unit (D15):
    /// density K/M3, KG/M3 to g/cc and neutron %, PU to v/v. Gamma ray and resistivity keep their units.
    /// Curves outside the four families are not touched.
    /// </summary>
    public class ConvertCurveUnitsStep : IConditioningStep
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

                if (string.IsNullOrEmpty(curve.Unit))
                {
                    log.Add(Severity.Warning,
                        $"{curve.Mnemonic} has no unit; values not converted and assumed to be {info.StandardUnit}",
                        curve.Info.line, "~C", "", curve.Mnemonic);
                    continue;
                }

                foreach (UnitConversion conversion in info.Conversions)
                {
                    if (CurveCatalog.SameName(curve.Unit, conversion.Unit))
                    {
                        Divide(curve.Values, conversion.Divisor);
                        log.Add(Severity.Change,
                            $"{curve.Mnemonic} converted from {curve.Unit} to {info.StandardUnit} (/ {LasText.FormatNumber(conversion.Divisor)})",
                            0, "", "", curve.Mnemonic);
                        curve.Unit = info.StandardUnit;
                        break;
                    }
                }
            }
            return log.Entries;
        }

        /// <summary>NaN stays NaN, so missing values stay missing.</summary>
        private static void Divide(float[] values, double divisor)
        {
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (float)(values[i] / divisor);
            }
        }
    }
}
