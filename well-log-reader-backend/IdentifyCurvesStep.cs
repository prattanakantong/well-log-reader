using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// Step 7: finds the curves of the four plotted families with the curve catalog (D15).
    /// A curve is selected by mnemonic and a unit check. Gamma ray, density and neutron get one
    /// curve each (first in catalog order); resistivity keeps every match, grouped deep/medium/shallow.
    /// </summary>
    public class IdentifyCurvesStep : IConditioningStep
    {
        public List<ReportEntry> Apply(Well well)
        {
            ReportLog log = new ReportLog(well.FileName, Stage.Condition);

            SelectOne(well, CurveCatalog.Find(CurveFamily.GammaRay), log);

            int resistivityCount = 0;
            resistivityCount += SelectAll(well, CurveCatalog.Find(CurveFamily.ResistivityDeep), log);
            resistivityCount += SelectAll(well, CurveCatalog.Find(CurveFamily.ResistivityMedium), log);
            resistivityCount += SelectAll(well, CurveCatalog.Find(CurveFamily.ResistivityShallow), log);
            if (resistivityCount == 0)
            {
                log.Add(Severity.Warning, "No resistivity curve found; track will show \"No data\"");
            }

            SelectOne(well, CurveCatalog.Find(CurveFamily.BulkDensity), log);
            Curve neutron = SelectOne(well, CurveCatalog.Find(CurveFamily.NeutronPorosity), log);

            if (neutron != null && CurveCatalog.SameName(neutron.Mnemonic, CurveCatalog.SandstoneNeutronMnemonic))
            {
                log.Add(Severity.Warning,
                    "Neutron porosity is NPSS (sandstone matrix); the plot scale assumes limestone",
                    0, "~C", "", neutron.Mnemonic);
            }

            return log.Entries;
        }

        /// <summary>
        /// Picks the first usable curve in catalog order and reports the choice when there
        /// was more than one candidate. Returns the chosen curve, or null.
        /// </summary>
        private static Curve SelectOne(Well well, CurveFamilyInfo info, ReportLog log)
        {
            List<Curve> candidates = FindCandidates(well, info, log);
            if (candidates.Count == 0)
            {
                log.Add(Severity.Warning, $"No {info.DisplayName} curve found; track will show \"No data\"");
                return null;
            }

            Curve chosen = candidates[0];
            chosen.Family = info.Family;

            string message = $"{chosen.Mnemonic} selected as {info.DisplayName}";
            if (candidates.Count > 1)
            {
                message += " (also found: " + JoinMnemonics(candidates, 1) + ")";
            }
            log.Add(Severity.Change, message, 0, "~C", "", chosen.Mnemonic);
            return chosen;
        }

        /// <summary>Selects every usable curve of a resistivity group. Returns how many were selected.</summary>
        private static int SelectAll(Well well, CurveFamilyInfo info, ReportLog log)
        {
            List<Curve> candidates = FindCandidates(well, info, log);
            foreach (Curve curve in candidates)
            {
                curve.Family = info.Family;
            }
            if (candidates.Count > 0)
            {
                log.Add(Severity.Change, $"{JoinMnemonics(candidates, 0)} selected as {info.DisplayName}", 0, "~C");
            }
            return candidates.Count;
        }

        /// <summary>
        /// Curves whose mnemonic is in the family list, in catalog priority order (file order for
        /// equal mnemonics). A curve with a unit that is not allowed is skipped with a warning (D15).
        /// </summary>
        private static List<Curve> FindCandidates(Well well, CurveFamilyInfo info, ReportLog log)
        {
            List<Curve> candidates = new List<Curve>();
            foreach (string mnemonic in info.Mnemonics)
            {
                foreach (Curve curve in well.Curves)
                {
                    if (curve.Family != CurveFamily.None || !CurveCatalog.SameName(curve.Mnemonic, mnemonic))
                    {
                        continue;
                    }

                    bool hasUnit = !string.IsNullOrEmpty(curve.Unit);
                    if (hasUnit && !CurveCatalog.IsInList(curve.Unit, info.AllowedUnits))
                    {
                        log.Add(Severity.Warning,
                            $"{curve.Mnemonic} has unit \"{curve.Unit}\", which is not a {info.DisplayName} unit; not selected",
                            curve.Info.line, "~C", "", curve.Mnemonic);
                        continue;
                    }
                    candidates.Add(curve);
                }
            }
            return candidates;
        }

        private static string JoinMnemonics(List<Curve> curves, int startIndex)
        {
            string text = "";
            for (int i = startIndex; i < curves.Count; i++)
            {
                text += (text.Length > 0 ? ", " : "") + curves[i].Mnemonic;
            }
            return text;
        }
    }
}
