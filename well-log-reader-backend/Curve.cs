namespace well_log_reader_backend
{
    /// <summary>
    /// One log curve (one data column) of a well.
    /// Values[i] belongs to Well.Depth[i]; curves are aligned by row index, not by depth value (D2).
    /// </summary>
    public class Curve
    {
        /// <summary>The ~C line that defines this curve, exactly as read from the file.</summary>
        public Header Info { get; set; }

        /// <summary>
        /// One value per row, same length as Well.Depth.
        /// float is enough for 4-5 significant digits and halves memory (D4).
        /// Missing values are float.NaN (D3); check them with float.IsNaN, never ==.
        /// </summary>
        public float[] Values { get; set; } = new float[0];

        /// <summary>
        /// Current unit of Values. Starts as Info.unit and changes only when
        /// conditioning converts the values (D15), so Info always keeps the original.
        /// </summary>
        public string Unit { get; set; } = "";

        /// <summary>Family chosen by the curve catalog; None until conditioning step 7 (D15).</summary>
        public CurveFamily Family { get; set; } = CurveFamily.None;

        /// <summary>Short-cut for Info.mnem.</summary>
        public string Mnemonic
        {
            get { return Info == null ? "" : Info.mnem; }
        }
    }
}
