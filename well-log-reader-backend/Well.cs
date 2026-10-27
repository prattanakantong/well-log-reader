using System;
using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// Everything loaded from one LAS file.
    /// All curves share one depth array and are aligned by row index (D2).
    /// </summary>
    public class Well
    {
        /// <summary>Full path of the source file.</summary>
        public string FilePath { get; set; } = "";

        /// <summary>File name only, for example "WELL-A.las"; used in the report and plot title.</summary>
        public string FileName { get; set; } = "";

        /// <summary>Raw VERS value from ~V, for example "2.0". Used by the reader and the report only.</summary>
        public string Version { get; set; } = "";

        /// <summary>True when ~V says WRAP YES (one data row spread over several lines).</summary>
        public bool IsWrapped { get; set; }

        // Header lines are kept per section, in file order, duplicates included (D5).

        /// <summary>All ~V lines in file order.</summary>
        public List<Header> VersionItems { get; set; } = new List<Header>();

        /// <summary>All ~W lines in file order.</summary>
        public List<Header> WellItems { get; set; } = new List<Header>();

        /// <summary>All ~C lines in file order. The first one defines the depth index (first data column).</summary>
        public List<Header> CurveItems { get; set; } = new List<Header>();

        /// <summary>All ~P lines in file order.</summary>
        public List<Header> ParameterItems { get; set; } = new List<Header>();

        /// <summary>Raw text of the ~O section; it is free text, so it is not split.</summary>
        public string OtherText { get; set; } = "";

        /// <summary>Raw text of sections the reader does not know, kept so nothing is lost.</summary>
        public string UnknownSectionsText { get; set; } = "";

        /// <summary>
        /// Depth of every row, read from the first data column, never computed from STRT/STEP (D1).
        /// double avoids precision problems in comparison and unit conversion (D4).
        /// </summary>
        public double[] Depth { get; set; } = new double[0];

        /// <summary>
        /// Depth unit after conditioning: "m" when the unit was known and converted (D13),
        /// empty when the unit is unknown and the depth was not converted.
        /// </summary>
        public string DepthUnit { get; set; } = "";

        /// <summary>
        /// False when the first column is not depth (for example TIME). Such a well is loaded
        /// but not plotted (D12).
        /// </summary>
        public bool IsDepthIndexed { get; set; } = true;

        /// <summary>
        /// The data columns after the first one, in file order. The first column is not here
        /// because it is stored once, as double, in Depth (D2, D4).
        /// </summary>
        public List<Curve> Curves { get; set; } = new List<Curve>();

        // Convenience accessors, so nobody has to type mnemonic strings elsewhere (D5).
        // Text items return the value; items that need a unit or a number return the whole Header.
        // If a mnemonic appears more than once the first one is returned; the reader reports the duplicate.

        /// <summary>Well name (WELL value), or empty if missing.</summary>
        public string WellName
        {
            get { return ValueOf(FindWellItem("WELL")); }
        }

        /// <summary>Unique well identifier (UWI value), or empty if missing.</summary>
        public string Uwi
        {
            get { return ValueOf(FindWellItem("UWI")); }
        }

        /// <summary>Operating company (COMP value), or empty if missing.</summary>
        public string Company
        {
            get { return ValueOf(FindWellItem("COMP")); }
        }

        /// <summary>The NULL line of ~W, or null if missing (then -999.25 is used, D11).</summary>
        public Header NullItem
        {
            get { return FindWellItem("NULL"); }
        }

        /// <summary>The STRT line of ~W, or null if missing. Used only to check the data (D1).</summary>
        public Header StartItem
        {
            get { return FindWellItem("STRT"); }
        }

        /// <summary>The STOP line of ~W, or null if missing. Used only to check the data (D1).</summary>
        public Header StopItem
        {
            get { return FindWellItem("STOP"); }
        }

        /// <summary>The STEP line of ~W, or null if missing. Used only to check the data (D1).</summary>
        public Header StepItem
        {
            get { return FindWellItem("STEP"); }
        }

        /// <summary>
        /// Returns the first ~W line whose mnemonic matches (case-insensitive, trimmed), or null.
        /// </summary>
        public Header FindWellItem(string mnemonic)
        {
            return FindFirst(WellItems, mnemonic);
        }

        /// <summary>
        /// Returns the first ~V line whose mnemonic matches (case-insensitive, trimmed), or null.
        /// </summary>
        public Header FindVersionItem(string mnemonic)
        {
            return FindFirst(VersionItems, mnemonic);
        }

        private static Header FindFirst(List<Header> items, string mnemonic)
        {
            string wanted = mnemonic.Trim();
            foreach (Header item in items)
            {
                if (item.mnem != null &&
                    string.Equals(item.mnem.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }
            return null;
        }

        private static string ValueOf(Header item)
        {
            if (item == null || item.value == null)
            {
                return "";
            }
            return item.value;
        }
    }
}
