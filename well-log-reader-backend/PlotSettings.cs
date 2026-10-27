namespace well_log_reader_backend
{
    /// <summary>
    /// All sizes and track scales of the log plot in one place (D17).
    /// Every image has the same size, so plots of different wells can be compared side by side.
    /// </summary>
    public static class PlotSettings
    {
        public const int ImageWidth = 1100;
        public const int ImageHeight = 2000;

        /// <summary>Height of the title band above the tracks.</summary>
        public const int TitleHeight = 90;

        /// <summary>Width reserved on the left of the first track for the depth labels.</summary>
        public const int DepthLabelWidth = 90;

        /// <summary>Space inside each track for axis labels; the same for every track so the data areas line up.</summary>
        public const int TrackPaddingTop = 110;
        public const int TrackPaddingBottom = 70;
        public const int TrackPaddingSide = 20; // wide enough for the edge tick labels, e.g. "2000"

        // Track scales (D17).
        public const double GammaRayMin = 0;
        public const double GammaRayMax = 150;

        public const double ResistivityMin = 0.2;
        public const double ResistivityMax = 2000;

        public const double DensityMin = 1.95;
        public const double DensityMax = 2.95;

        /// <summary>Neutron is drawn reversed: 0.45 on the left, -0.15 on the right (limestone-compatible).</summary>
        public const double NeutronLeft = 0.45;
        public const double NeutronRight = -0.15;
    }
}
