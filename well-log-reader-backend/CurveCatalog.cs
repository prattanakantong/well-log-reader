using System;

namespace well_log_reader_backend
{
    /// <summary>
    /// The curve catalog: the one place to edit which mnemonics and units belong to the
    /// four plotted families, how units are converted, and the physical limits (D15, D16).
    ///
    /// Lookalikes are left out on purpose and must never be added: DRHO (density correction),
    /// DPHI (density porosity), POTA/THOR/URAN (elements) and count-rate curves (D15).
    /// Do not add an alias just because it appears in a sample file; check its meaning first
    /// (Energistics PWLS or the SLB Curve Mnemonic Dictionary).
    /// </summary>
    public static class CurveCatalog
    {
        private static readonly string[] ResistivityUnits = { "OHMM", "OHM.M", "OHM-M", "OHM_M" };

        /// <summary>Families in the order they are searched. Mnemonics within a family are in priority order.</summary>
        public static readonly CurveFamilyInfo[] Families =
        {
            new CurveFamilyInfo
            {
                Family = CurveFamily.GammaRay,
                DisplayName = "gamma ray",
                Mnemonics = new[] { "GR", "GRC", "ECGR", "SGR", "HSGR", "GAM", "GAMMA", "CGR", "HCGR" },
                AllowedUnits = new[] { "GAPI", "API" },
                StandardUnit = "GAPI",
                IsImpossible = value => value < 0,
                ImpossibleText = "< 0",
                UsualMax = 500
            },
            new CurveFamilyInfo
            {
                Family = CurveFamily.ResistivityDeep,
                DisplayName = "deep resistivity",
                Mnemonics = new[] { "ILD", "RILD", "IDPH", "LLD", "RLLD", "RT", "RD", "RDEP",
                                    "AT90", "AHT90", "AF90", "AO90", "M2R9" },
                AllowedUnits = ResistivityUnits,
                StandardUnit = "OHMM",
                IsImpossible = value => value <= 0,
                ImpossibleText = "<= 0"
            },
            new CurveFamilyInfo
            {
                Family = CurveFamily.ResistivityMedium,
                DisplayName = "medium resistivity",
                Mnemonics = new[] { "ILM", "RILM", "IMPH", "AT60", "AT30" },
                AllowedUnits = ResistivityUnits,
                StandardUnit = "OHMM",
                IsImpossible = value => value <= 0,
                ImpossibleText = "<= 0"
            },
            new CurveFamilyInfo
            {
                Family = CurveFamily.ResistivityShallow,
                DisplayName = "shallow resistivity",
                Mnemonics = new[] { "LLS", "SFLU", "SFL", "SFLA", "AT20", "AT10", "MSFL", "RXOZ", "RXO8", "MCFL" },
                AllowedUnits = ResistivityUnits,
                StandardUnit = "OHMM",
                IsImpossible = value => value <= 0,
                ImpossibleText = "<= 0"
            },
            new CurveFamilyInfo
            {
                Family = CurveFamily.BulkDensity,
                DisplayName = "bulk density",
                Mnemonics = new[] { "RHOB", "RHOZ", "DEN", "DENS", "ZDEN" },
                AllowedUnits = new[] { "G/C3", "G/CC", "GM/CC", "G/CM3", "K/M3", "KG/M3" },
                StandardUnit = "G/CC",
                Conversions = new[] { new UnitConversion("K/M3", 1000), new UnitConversion("KG/M3", 1000) },
                IsImpossible = value => value <= 0,
                ImpossibleText = "<= 0",
                UsualMin = 1.0,
                UsualMax = 3.5
            },
            new CurveFamilyInfo
            {
                Family = CurveFamily.NeutronPorosity,
                DisplayName = "neutron porosity",
                Mnemonics = new[] { "NPHI", "TNPH", "NPOR", "CNL", "NPLS", "NPSS" },
                // CFCF (cubic feet per cubic foot), FT3/FT3 and M3/M3 are volume per volume, the same as V/V.
                AllowedUnits = new[] { "V/V", "DEC", "FRAC", "CFCF", "FT3/FT3", "M3/M3", "%", "PU" },
                StandardUnit = "V/V",
                Conversions = new[] { new UnitConversion("%", 100), new UnitConversion("PU", 100) },
                UsualMin = -0.15,
                UsualMax = 1.0
            }
        };

        /// <summary>Neutron porosity on a sandstone matrix; the plot scale assumes limestone (D15).</summary>
        public const string SandstoneNeutronMnemonic = "NPSS";

        /// <summary>The catalog entry of a family, or null for CurveFamily.None.</summary>
        public static CurveFamilyInfo Find(CurveFamily family)
        {
            foreach (CurveFamilyInfo info in Families)
            {
                if (info.Family == family)
                {
                    return info;
                }
            }
            return null;
        }

        /// <summary>Case-insensitive, trimmed comparison used for mnemonics and units (D15).</summary>
        public static bool SameName(string a, string b)
        {
            if (a == null || b == null)
            {
                return false;
            }
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True when the name is in the list (case-insensitive, trimmed).</summary>
        public static bool IsInList(string name, string[] list)
        {
            foreach (string item in list)
            {
                if (SameName(name, item))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
