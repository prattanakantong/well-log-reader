using System;

namespace well_log_reader_backend
{
    /// <summary>
    /// Everything the program knows about one curve family: which mnemonics belong to it,
    /// which units are allowed, how to convert them, and which values are impossible or unusual.
    /// The actual values live in CurveCatalog (D15, D16).
    /// </summary>
    public class CurveFamilyInfo
    {
        public CurveFamily Family { get; set; }

        /// <summary>Name used in report messages, for example "gamma ray".</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>Mnemonics in priority order: the first match wins (D15).</summary>
        public string[] Mnemonics { get; set; } = new string[0];

        /// <summary>A curve with a unit outside this list is not selected (D15).</summary>
        public string[] AllowedUnits { get; set; } = new string[0];

        /// <summary>Unit written on the curve after a conversion, for example "G/CC".</summary>
        public string StandardUnit { get; set; } = "";

        /// <summary>Units that are divided to reach the standard unit, for example "KG/M3" by 1000.</summary>
        public UnitConversion[] Conversions { get; set; } = new UnitConversion[0];

        /// <summary>True for values that cannot exist physically; they become NaN (D16).</summary>
        public Func<double, bool> IsImpossible { get; set; } = value => false;

        /// <summary>Lowest usual value in the standard unit; values below only get a warning (D16).</summary>
        public double UsualMin { get; set; } = double.NegativeInfinity;

        /// <summary>Highest usual value in the standard unit; values above only get a warning (D16).</summary>
        public double UsualMax { get; set; } = double.PositiveInfinity;

        /// <summary>Text for report messages, for example "<= 0".</summary>
        public string ImpossibleText { get; set; } = "";
    }

    /// <summary>One unit that is converted to the family's standard unit by dividing by Divisor.</summary>
    public class UnitConversion
    {
        public UnitConversion(string unit, double divisor)
        {
            Unit = unit;
            Divisor = divisor;
        }

        public string Unit { get; }

        public double Divisor { get; }
    }
}
