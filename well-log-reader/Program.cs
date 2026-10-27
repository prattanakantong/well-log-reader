using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using well_log_reader_backend;

namespace WellLogReader
{
    /// <summary>
    /// Command line front end: checks the arguments, finds the LAS files and processes them
    /// one after another (D18). All real work happens in the backend library.
    /// </summary>
    internal class Program
    {
        private const int ExitOk = 0;
        private const int ExitUnexpectedFailure = 1;
        private const int ExitInvalidArguments = 2;

        static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (Exception ex)
            {
                // Each file has its own catch in WellProcessor; this only catches failures of the whole run.
                Console.Error.WriteLine("Unexpected failure: " + ex);
                return ExitUnexpectedFailure;
            }
        }

        private static int Run(string[] args)
        {
            if (args.Length != 2)
            {
                PrintUsage();
                return ExitInvalidArguments;
            }

            string inputFolder = args[0];
            string outputFolder = args[1];

            if (!Directory.Exists(inputFolder))
            {
                Console.Error.WriteLine("Input folder not found: " + inputFolder);
                PrintUsage();
                return ExitInvalidArguments;
            }

            string plotsFolder = Path.Combine(outputFolder, "plots");
            Directory.CreateDirectory(plotsFolder);

            List<string> files = FindLasFiles(inputFolder);
            if (files.Count == 0)
            {
                Console.WriteLine("No *.las files found in " + inputFolder);
                return ExitOk;
            }

            Stopwatch totalTime = Stopwatch.StartNew();
            List<WellSummary> summaries = new List<WellSummary>();

            for (int i = 0; i < files.Count; i++)
            {
                WellSummary summary = WellProcessor.Process(files[i], plotsFolder);
                summaries.Add(summary);
                PrintProgress(i + 1, files.Count, summary);
            }

            totalTime.Stop();
            RunStatistics stats = new RunStatistics(summaries, totalTime.Elapsed);

            string markdownPath = Path.Combine(outputFolder, "report.md");
            string csvPath = Path.Combine(outputFolder, "report.csv");
            ReportWriter.WriteMarkdown(markdownPath, summaries, stats, inputFolder);
            ReportWriter.WriteCsv(csvPath, summaries);

            PrintRunSummary(stats);
            Console.WriteLine("Reports:        " + markdownPath + ", " + csvPath);
            return ExitOk;
        }

        /// <summary>
        /// All *.las files in the folder (not recursive), case-insensitive, sorted by name.
        /// The extension is checked exactly because Windows also matches "*.las" to longer
        /// extensions such as ".lasx".
        /// </summary>
        private static List<string> FindLasFiles(string folder)
        {
            List<string> files = new List<string>();
            foreach (string path in Directory.GetFiles(folder))
            {
                if (string.Equals(Path.GetExtension(path), ".las", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(path);
                }
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Usage: well-log-reader.exe <inputFolder> <outputFolder>");
        }

        /// <summary>One line per file, for example "[ 12/50] WELL-A.las  OK  (3 warnings)".</summary>
        private static void PrintProgress(int number, int total, WellSummary summary)
        {
            string counter = "[" + number.ToString().PadLeft(total.ToString().Length) + "/" + total + "]";
            string line = counter + " " + summary.FileName + "  " + StatusText(summary.Status);
            if (summary.WarningCount > 0)
            {
                line += "  (" + summary.WarningCount + " warning" + (summary.WarningCount == 1 ? "" : "s") + ")";
            }
            Console.WriteLine(line);
        }

        private static string StatusText(WellStatus status)
        {
            switch (status)
            {
                case WellStatus.Ok: return "OK";
                case WellStatus.Warnings: return "OK";
                case WellStatus.NotPlotted: return "NOT PLOTTED";
                default: return "REJECTED";
            }
        }

        private static void PrintRunSummary(RunStatistics stats)
        {
            Console.WriteLine();
            Console.WriteLine("Files:          " + stats.FileCount);
            Console.WriteLine("  OK:           " + stats.OkCount);
            Console.WriteLine("  With warnings: " + stats.WarningsCount);
            Console.WriteLine("  Not plotted:  " + stats.NotPlottedCount);
            Console.WriteLine("  Rejected:     " + stats.RejectedCount);
            Console.WriteLine("Total time:     " + Seconds(stats.TotalTime) +
                              "  (load " + Seconds(stats.LoadTime) +
                              ", condition " + Seconds(stats.ConditionTime) +
                              ", plot " + Seconds(stats.PlotTime) + ")");
        }

        private static string Seconds(TimeSpan time)
        {
            return time.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }
    }
}
