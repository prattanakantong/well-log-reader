using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// Step 3: converts depth to metres (D13).
    /// Unit source: the unit of the first ~C curve; if empty, the unit of STRT in ~W.
    /// STRT/STOP/STEP are converted too, so step 6 compares them in the same unit.
    /// </summary>
    public class DepthToMetresStep : IConditioningStep
    {
        public const double MetresPerFoot = 0.3048;

        public List<ReportEntry> Apply(Well well)
        {
            ReportLog log = new ReportLog(well.FileName, Stage.Condition);

            Header indexCurve = well.CurveItems.Count > 0 ? well.CurveItems[0] : null;
            string unit = indexCurve != null ? indexCurve.unit : "";
            string unitSource = "~C";
            if (string.IsNullOrEmpty(unit) && well.StartItem != null)
            {
                unit = well.StartItem.unit;
                unitSource = "STRT";
            }

            double factor;
            if (DepthIndex.IsFeet(unit))
            {
                factor = MetresPerFoot;
                well.DepthUnit = "m";
            }
            else if (DepthIndex.IsMetres(unit))
            {
                factor = 1.0;
                well.DepthUnit = "m";
            }
            else
            {
                factor = 1.0;
                well.DepthUnit = "";
            }

            bool depthInMetres = well.DepthUnit == "m";
            well.HeaderStart = ReadHeaderDepth(well.StartItem, factor, depthInMetres);
            well.HeaderStop = ReadHeaderDepth(well.StopItem, factor, depthInMetres);
            well.HeaderStep = ReadHeaderDepth(well.StepItem, factor, depthInMetres);

            if (!well.IsDepthIndexed)
            {
                // A TIME (or other) index is not a depth; the reader already reported it (D12).
                well.DepthUnit = "";
                return log.Entries;
            }

            if (well.DepthUnit == "")
            {
                string shown = string.IsNullOrEmpty(unit) ? "none" : "\"" + unit + "\"";
                log.Add(Severity.Warning, $"Depth unit unknown ({shown}); depth not converted, plot labelled \"unit unknown\"");
                return log.Entries;
            }

            if (factor != 1.0)
            {
                double[] depth = well.Depth;
                for (int i = 0; i < depth.Length; i++)
                {
                    depth[i] = depth[i] * factor;
                }
                log.Add(Severity.Change,
                    $"Depth converted from {unit} (from {unitSource}) to m (x {LasText.FormatNumber(factor)})",
                    0, "", "", indexCurve != null ? indexCurve.mnem : "");
            }
            return log.Entries;
        }

        /// <summary>
        /// Parses STRT/STOP/STEP. When depth is in metres, a line with its own length unit uses
        /// that unit and a line without one is taken to be in the depth unit. When the depth unit
        /// is unknown nothing is converted, so header and data stay comparable.
        /// </summary>
        private static double ReadHeaderDepth(Header item, double depthFactor, bool depthInMetres)
        {
            double value;
            if (item == null || !LasText.TryParseNumber(item.value, out value))
            {
                return double.NaN;
            }
            if (!depthInMetres)
            {
                return value;
            }
            if (DepthIndex.IsFeet(item.unit))
            {
                return value * MetresPerFoot;
            }
            if (DepthIndex.IsMetres(item.unit))
            {
                return value;
            }
            return value * depthFactor;
        }
    }
}
