using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OSDP.Net.LineQuality.Cli
{
    /// <summary>
    /// Renders a <see cref="LineQualityReport"/> for a terminal, and optionally as JSON.
    /// </summary>
    internal static class ReportWriter
    {
        public static void WriteSummary(LineQualityReport report)
        {
            var output = Console.Out;

            output.WriteLine();
            output.WriteLine("OSDP Line Quality Test");
            output.WriteLine(new string('=', 78));
            output.WriteLine($"Connection : {report.Connection}");
            output.WriteLine($"Address    : {report.Address} (0x{report.Address:X2})");
            output.WriteLine($"Profile    : {report.Profile} " +
                             $"({report.IterationsPerCombination} iterations per combination)");
            output.WriteLine($"Started    : {report.StartedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
            output.WriteLine($"Duration   : {FormatDuration(report.Duration)}");
            output.WriteLine();

            output.WriteLine("  Baud Rate   Sent  Failed   Success   Detects   Max ms   Result");
            output.WriteLine("  " + new string('-', 66));

            foreach (var result in report.BaudRates)
            {
                if (!result.WasTested)
                {
                    output.WriteLine($"  {result.BaudRate,9}      -       -         -         -        -   " +
                                     $"{Describe(result.Verdict)}");
                    continue;
                }

                output.WriteLine(
                    $"  {result.BaudRate,9} {result.PacketsSent,6} {result.Failures,7} " +
                    $"{result.SuccessRatePercent,8:F2}% {result.DetectionLimitPercent,8:F2}% " +
                    $"{result.MaxResponseTimeMs,8:F1}   {Describe(result.Verdict)}");
            }

            output.WriteLine();
            WriteSkipReasons(report);
            WriteErrorBreakdown(report);
            WriteConclusion(report);
        }

        private static void WriteSkipReasons(LineQualityReport report)
        {
            var failed = report.BaudRates.Where(result => result.FailureReason != null).ToArray();
            if (failed.Length > 0)
            {
                Console.Out.WriteLine("Rate changes that did not complete:");
                foreach (var result in failed)
                {
                    Console.Out.WriteLine($"  {result.BaudRate,9}  {result.FailureReason}");
                }

                Console.Out.WriteLine();
            }

            var skipped = report.BaudRates.Where(result => result.SkipReason != null).ToArray();
            if (skipped.Length == 0) return;

            Console.Out.WriteLine("Not tested:");
            foreach (var result in skipped)
            {
                Console.Out.WriteLine($"  {result.BaudRate,9}  {result.SkipReason}");
            }

            Console.Out.WriteLine();
        }

        private static void WriteErrorBreakdown(LineQualityReport report)
        {
            var withFailures = report.BaudRates.Where(result => result.WasTested && result.Failures > 0)
                .ToArray();
            if (withFailures.Length == 0) return;

            // The taxonomy is the diagnostic payload: a timeout, a bad CRC and a pattern mismatch
            // point at an open line, marginal signal integrity, and a responder fault respectively.
            Console.Out.WriteLine("Failure breakdown:");
            Console.Out.WriteLine("  Baud Rate   Timeouts   Integrity   NAKs   Mismatches");
            Console.Out.WriteLine("  " + new string('-', 54));

            foreach (var result in withFailures)
            {
                Console.Out.WriteLine(
                    $"  {result.BaudRate,9} {result.Timeouts,10} {result.IntegrityErrors,11} " +
                    $"{result.Naks,6} {result.PatternMismatches,12}");
            }

            Console.Out.WriteLine();
        }

        private static void WriteConclusion(LineQualityReport report)
        {
            var output = Console.Out;

            if (report.RecommendedBaudRate.HasValue)
            {
                output.WriteLine($"Recommended baud rate: {report.RecommendedBaudRate.Value}");
            }
            else
            {
                output.WriteLine("Recommended baud rate: none - no rate passed cleanly.");
            }

            output.WriteLine($"Overall: {Describe(report.OverallVerdict)}");

            var best = report.BaudRates
                .Where(result => result.WasTested)
                .OrderByDescending(result => result.PacketsSent)
                .FirstOrDefault();

            if (best != null && report.OverallVerdict == LineQualityVerdict.Pass)
            {
                // A pass bounds the loss rate, it does not prove zero loss. Saying so here keeps a
                // screening run from being filed as a commissioning result.
                output.WriteLine();
                output.WriteLine(
                    $"A pass at this profile establishes only that packet loss is below " +
                    $"{best.DetectionLimitPercent:F2}%.");

                if (report.Profile != TestProfile.Extended)
                {
                    output.WriteLine(
                        "Run --profile extended at the selected rate to substantiate a 99.9% claim.");
                }
            }

            output.WriteLine();
        }

        public static void WriteJson(LineQualityReport report, string path)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

            File.WriteAllText(path, JsonSerializer.Serialize(report, options));
            Console.Out.WriteLine($"Wrote {path}");
        }

        public static void WriteMarkdown(LineQualityReport report, string path,
            LineQualityReportMetadata metadata)
        {
            File.WriteAllText(path, LineQualityMarkdownReport.Render(report, metadata));
            Console.Out.WriteLine($"Wrote {path}");
        }

        private static string Describe(LineQualityVerdict verdict)
        {
            switch (verdict)
            {
                case LineQualityVerdict.Pass: return "PASS";
                case LineQualityVerdict.Marginal: return "MARGINAL";
                case LineQualityVerdict.Fail: return "FAIL";
                default: return "UNTESTED";
            }
        }

        private static string FormatDuration(TimeSpan duration) =>
            duration.TotalMinutes >= 1
                ? $"{(int)duration.TotalMinutes}m {duration.Seconds}s"
                : duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s";
    }
}
