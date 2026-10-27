using System;
using System.Collections.Generic;
using System.IO;
using ScottPlot;
using SkiaSharp;

namespace well_log_reader_backend
{
    /// <summary>
    /// Draws one log plot (PNG) per well with ScottPlot 5 (D17).
    /// Each track is its own ScottPlot Plot; all tracks are drawn side by side on one Skia canvas.
    /// Every track uses the same fixed padding and the same depth range, so the tracks line up.
    /// Depth increases downward because the y-axis limits are set with the deepest value at the bottom.
    /// </summary>
    public static class LogPlotRenderer
    {
        /// <summary>Renders the well to a PNG file. The well must be depth-indexed and conditioned.</summary>
        public static void Render(Well well, string pngPath)
        {
            double top = well.Depth[0];
            double bottom = well.Depth[well.Depth.Length - 1];
            if (top == bottom)
            {
                bottom = top + 1; // a single row still needs a visible depth range
            }

            string depthUnit = well.DepthUnit == "m" ? "m" : "unit unknown";

            int trackWidth = (PlotSettings.ImageWidth - PlotSettings.DepthLabelWidth) / 3;
            int firstTrackWidth = PlotSettings.DepthLabelWidth + trackWidth;

            Plot gammaRay = CreateGammaRayTrack(well, depthUnit);
            Plot resistivity = CreateResistivityTrack(well);
            Plot densityNeutron = CreateDensityNeutronTrack(well);

            Plot[] tracks = { gammaRay, resistivity, densityNeutron };
            foreach (Plot track in tracks)
            {
                track.Axes.SetLimitsY(bottom, top); // bottom of the image = deepest depth
            }

            SKImageInfo info = new SKImageInfo(PlotSettings.ImageWidth, PlotSettings.ImageHeight);
            using (SKSurface surface = SKSurface.Create(info))
            {
                SKCanvas canvas = surface.Canvas;
                canvas.Clear(SKColors.White);

                // Each track is rendered on its own surface first, because ScottPlot paints its
                // background over the whole canvas it is given; then the tracks are placed side by side.
                int x = 0;
                int trackHeight = PlotSettings.ImageHeight - PlotSettings.TitleHeight;
                for (int i = 0; i < tracks.Length; i++)
                {
                    int width = i == 0 ? firstTrackWidth : trackWidth;
                    using (SKSurface trackSurface = SKSurface.Create(new SKImageInfo(width, trackHeight)))
                    {
                        tracks[i].Render(trackSurface);
                        using (SKImage trackImage = trackSurface.Snapshot())
                        {
                            canvas.DrawImage(trackImage, x, PlotSettings.TitleHeight);
                        }
                    }
                    x += width;
                }

                DrawTitle(canvas, well, depthUnit);

                using (SKImage image = surface.Snapshot())
                using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                {
                    File.WriteAllBytes(pngPath, data.ToArray());
                }
            }
        }

        private static Plot CreateGammaRayTrack(Well well, string depthUnit)
        {
            Plot plot = CreateTrack(PlotSettings.DepthLabelWidth);
            plot.Axes.Left.Label.Text = "Depth (" + depthUnit + ")";
            plot.Axes.SetLimitsX(PlotSettings.GammaRayMin, PlotSettings.GammaRayMax);

            Curve gr = FindFirst(well, CurveFamily.GammaRay);
            if (gr == null)
            {
                plot.Axes.Bottom.Label.Text = "Gamma ray";
                ShowNoData(plot);
                return plot;
            }

            plot.Axes.Bottom.Label.Text = gr.Mnemonic + " (" + UnitText(gr) + ")";
            AddCurve(plot, well.Depth, gr.Values, false, Colors.Green, null, plot.Axes.Bottom);
            return plot;
        }

        /// <summary>
        /// Resistivity on a log scale: log10 of the values is plotted and the tick labels show the
        /// real values. Every selected resistivity curve is drawn, styled by group (D15, D17).
        /// </summary>
        private static Plot CreateResistivityTrack(Well well)
        {
            Plot plot = CreateTrack(PlotSettings.TrackPaddingSide);
            HideDepthLabels(plot);
            plot.Axes.Bottom.Label.Text = "Resistivity (ohm.m)";
            plot.Axes.SetLimitsX(Math.Log10(PlotSettings.ResistivityMin), Math.Log10(PlotSettings.ResistivityMax));
            plot.Axes.Bottom.TickGenerator = LogTicks(PlotSettings.ResistivityMin, PlotSettings.ResistivityMax);

            int count = 0;
            // Each group has its own colour. Solid lines are used everywhere because dashed or
            // dotted lines turn into noise on dense log data.
            foreach (Curve curve in well.Curves)
            {
                string group;
                Color color;
                if (curve.Family == CurveFamily.ResistivityDeep)
                {
                    group = "deep"; color = Colors.Red;
                }
                else if (curve.Family == CurveFamily.ResistivityMedium)
                {
                    group = "medium"; color = Colors.Blue;
                }
                else if (curve.Family == CurveFamily.ResistivityShallow)
                {
                    group = "shallow"; color = Colors.DarkOrange;
                }
                else
                {
                    continue;
                }

                AddCurve(plot, well.Depth, curve.Values, true, color, curve.Mnemonic + " (" + group + ")", plot.Axes.Bottom);
                count++;
            }

            if (count == 0)
            {
                ShowNoData(plot);
            }
            else
            {
                plot.ShowLegend(Alignment.UpperRight);
            }
            return plot;
        }

        /// <summary>
        /// Density on the bottom axis and neutron on the top axis of the same track. The neutron
        /// axis is reversed (0.45 left, -0.15 right) so the two scales are limestone-compatible (D17).
        /// </summary>
        private static Plot CreateDensityNeutronTrack(Well well)
        {
            Plot plot = CreateTrack(PlotSettings.TrackPaddingSide);
            HideDepthLabels(plot);
            plot.Axes.SetLimitsX(PlotSettings.DensityMin, PlotSettings.DensityMax, plot.Axes.Bottom);
            plot.Axes.SetLimitsX(PlotSettings.NeutronLeft, PlotSettings.NeutronRight, plot.Axes.Top);
            plot.Axes.Bottom.Label.Text = "Bulk density (g/cc)";
            plot.Axes.Top.Label.Text = "Neutron porosity (v/v)";

            Curve density = FindFirst(well, CurveFamily.BulkDensity);
            Curve neutron = FindFirst(well, CurveFamily.NeutronPorosity);

            if (density != null)
            {
                plot.Axes.Bottom.Label.Text = density.Mnemonic + " (" + UnitText(density) + ")";
                AddCurve(plot, well.Depth, density.Values, false, Colors.Red, density.Mnemonic, plot.Axes.Bottom);
            }
            if (neutron != null)
            {
                plot.Axes.Top.Label.Text = neutron.Mnemonic + " (" + UnitText(neutron) + ")";
                AddCurve(plot, well.Depth, neutron.Values, false, Colors.Blue, neutron.Mnemonic, plot.Axes.Top);
            }

            if (density == null && neutron == null)
            {
                ShowNoData(plot);
            }
            else
            {
                plot.ShowLegend(Alignment.UpperRight);
            }
            return plot;
        }

        /// <summary>A track with the fixed padding used by every track, so all data areas line up.</summary>
        private static Plot CreateTrack(int paddingLeft)
        {
            Plot plot = new Plot();
            plot.Layout.Fixed(new PixelPadding(paddingLeft, PlotSettings.TrackPaddingSide,
                                               PlotSettings.TrackPaddingBottom, PlotSettings.TrackPaddingTop));
            return plot;
        }

        /// <summary>Only the first track shows depth numbers; the others keep the grid lines.</summary>
        private static void HideDepthLabels(Plot plot)
        {
            plot.Axes.Left.TickLabelStyle.IsVisible = false;
        }

        private static void ShowNoData(Plot plot)
        {
            plot.Add.Annotation("No data", Alignment.MiddleCenter);
        }

        /// <summary>
        /// Adds one curve as connected lines. NaN values are split out so they leave a gap
        /// instead of being joined across (D17).
        /// </summary>
        private static void AddCurve(Plot plot, double[] depth, float[] values, bool logScale,
                                     Color color, string legendText, IXAxis xAxis)
        {
            bool legendAdded = false;
            List<double> xs = new List<double>();
            List<double> ys = new List<double>();

            for (int i = 0; i <= values.Length; i++)
            {
                double value = i < values.Length ? values[i] : double.NaN;
                if (logScale)
                {
                    value = value > 0 ? Math.Log10(value) : double.NaN;
                }

                if (!double.IsNaN(value))
                {
                    xs.Add(value);
                    ys.Add(depth[i]);
                    continue;
                }

                // A NaN (or the end of the data) closes the current segment.
                if (xs.Count > 0)
                {
                    ScottPlot.Plottables.Scatter line = plot.Add.ScatterLine(xs.ToArray(), ys.ToArray(), color);
                    line.LineWidth = 1.2f;
                    line.Axes.XAxis = xAxis;
                    if (!legendAdded && legendText != null)
                    {
                        line.LegendText = legendText;
                        legendAdded = true;
                    }
                    xs.Clear();
                    ys.Clear();
                }
            }
        }

        /// <summary>Log-scale ticks: a labelled major tick at each decade starting at min, minor ticks in between.</summary>
        private static ScottPlot.TickGenerators.NumericManual LogTicks(double min, double max)
        {
            ScottPlot.TickGenerators.NumericManual ticks = new ScottPlot.TickGenerators.NumericManual();
            for (double major = min; major <= max * 1.0001; major *= 10)
            {
                ticks.AddMajor(Math.Log10(major), LasText.FormatNumber(major));
                for (int step = 2; step <= 9; step++)
                {
                    double minor = major * step;
                    if (minor < max)
                    {
                        ticks.AddMinor(Math.Log10(minor));
                    }
                }
            }
            return ticks;
        }

        private static Curve FindFirst(Well well, CurveFamily family)
        {
            foreach (Curve curve in well.Curves)
            {
                if (curve.Family == family)
                {
                    return curve;
                }
            }
            return null;
        }

        private static string UnitText(Curve curve)
        {
            return string.IsNullOrEmpty(curve.Unit) ? "no unit" : curve.Unit;
        }

        /// <summary>Title: well name, source file name and depth unit (D17).</summary>
        private static void DrawTitle(SKCanvas canvas, Well well, string depthUnit)
        {
            string wellName = string.IsNullOrEmpty(well.WellName) ? "(no well name)" : well.WellName;
            using (SKPaint paint = new SKPaint { Color = SKColors.Black, IsAntialias = true })
            using (SKFont titleFont = new SKFont(SKTypeface.Default, 30))
            using (SKFont subtitleFont = new SKFont(SKTypeface.Default, 18))
            {
                canvas.DrawText(wellName, 20, 40, SKTextAlign.Left, titleFont, paint);
                canvas.DrawText(well.FileName + "    depth: " + depthUnit, 20, 72, SKTextAlign.Left, subtitleFont, paint);
            }
        }
    }
}
