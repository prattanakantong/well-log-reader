using System;

namespace well_log_reader_backend
{
    /// <summary>
    /// Knows which mnemonics and units mean "depth", so the reader (D12) and the
    /// depth-unit step (D13) use the same lists.
    /// </summary>
    public static class DepthIndex
    {
        private static readonly string[] DepthMnemonics = { "DEPT", "DEPTH", "MD" };
        private static readonly string[] FeetUnits = { "F", "FT", "FEET", "FOOT" };
        private static readonly string[] MetreUnits = { "M", "METER", "METERS", "METRE", "METRES" };

        /// <summary>
        /// True when the first ~C curve is a depth index: its mnemonic looks like depth
        /// or its unit is a length (D12). A TIME index, for example, is not.
        /// </summary>
        public static bool LooksLikeDepth(Header firstCurve)
        {
            if (firstCurve == null)
            {
                return false;
            }
            return IsInList(firstCurve.mnem, DepthMnemonics) || IsFeet(firstCurve.unit) || IsMetres(firstCurve.unit);
        }

        /// <summary>True for feet units such as F, FT, FEET (D13).</summary>
        public static bool IsFeet(string unit)
        {
            return IsInList(unit, FeetUnits);
        }

        /// <summary>True for metre units such as M, METER(S), METRE(S) (D13).</summary>
        public static bool IsMetres(string unit)
        {
            return IsInList(unit, MetreUnits);
        }

        private static bool IsInList(string text, string[] list)
        {
            if (text == null)
            {
                return false;
            }
            string wanted = text.Trim();
            foreach (string item in list)
            {
                if (string.Equals(item, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
