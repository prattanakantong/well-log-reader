namespace well_log_reader_backend
{
    /// <summary>
    /// Rebuilds the rows of a well from a list of old row indexes. The same list is applied to the
    /// depth array and to every curve, so curves can never drift away from their depth (D2).
    /// </summary>
    public static class WellRows
    {
        /// <summary>
        /// New row i becomes old row rows[i]. Used to drop rows (shorter list) and to sort rows
        /// (a permutation of all indexes).
        /// </summary>
        public static void Keep(Well well, int[] rows)
        {
            double[] newDepth = new double[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                newDepth[i] = well.Depth[rows[i]];
            }
            well.Depth = newDepth;

            foreach (Curve curve in well.Curves)
            {
                float[] newValues = new float[rows.Length];
                for (int i = 0; i < rows.Length; i++)
                {
                    newValues[i] = curve.Values[rows[i]];
                }
                curve.Values = newValues;
            }
        }
    }
}
