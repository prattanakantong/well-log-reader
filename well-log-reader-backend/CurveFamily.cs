namespace well_log_reader_backend
{
    /// <summary>
    /// The measurement a curve represents, decided by the curve catalog (D15).
    /// Resistivity is split into deep / medium / shallow because each group
    /// is drawn with its own style on the same track.
    /// </summary>
    public enum CurveFamily
    {
        /// <summary>Not one of the plotted families; the curve keeps its original units.</summary>
        None,
        GammaRay,
        ResistivityDeep,
        ResistivityMedium,
        ResistivityShallow,
        BulkDensity,
        NeutronPorosity
    }
}
