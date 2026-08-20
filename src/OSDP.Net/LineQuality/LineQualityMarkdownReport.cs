using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OSDP.Net.LineQuality
{
    /// <summary>
    /// Details about an installation that the test cannot discover for itself, used to fill in the
    /// header of a written report.
    /// </summary>
    /// <remarks>
    /// Every field is optional. Anything left unset is rendered as a blank the technician can
    /// complete by hand, which keeps the output usable as the commissioning record described in
    /// section 7 of the test procedure.
    /// </remarks>
    public class LineQualityReportMetadata
    {
        /// <summary>Gets or sets who ran the test.</summary>
        public string TesterName { get; set; }

        /// <summary>Gets or sets where the installation is.</summary>
        public string InstallationLocation { get; set; }

        /// <summary>Gets or sets the cable type and length.</summary>
        public string CableDescription { get; set; }

        /// <summary>Gets or sets the model and firmware of the controller side.</summary>
        public string AcuDescription { get; set; }

        /// <summary>Gets or sets the model and firmware of the responder side.</summary>
        public string PdDescription { get; set; }

        /// <summary>Gets or sets the host platform and serial adapter used.</summary>
        public string AdapterDescription { get; set; }

        /// <summary>
        /// Gets or sets whether the serial adapter's latency timer was lowered before the run.
        /// </summary>
        /// <remarks>
        /// Recorded because it determines whether the response times in the report mean anything.
        /// See <see cref="LineQualityTest"/>.
        /// </remarks>
        public bool? AdapterLatencyTimerAdjusted { get; set; }

        /// <summary>Gets or sets free-form notes to include in the report.</summary>
        public string Notes { get; set; }
    }

    /// <summary>
    /// Renders a <see cref="LineQualityReport"/> as the Markdown report described in section 7 of
    /// the OSDP Line Quality Test Procedure.
    /// </summary>
    public static class LineQualityMarkdownReport
    {
        /// <summary>
        /// Renders the report.
        /// </summary>
        /// <param name="report">The results to render.</param>
        /// <param name="metadata">Optional installation details for the header.</param>
        /// <returns>The report as Markdown.</returns>
        /// <exception cref="ArgumentNullException">The report is null.</exception>
        public static string Render(LineQualityReport report, LineQualityReportMetadata metadata = null)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            var builder = new StringBuilder();
            metadata = metadata ?? new LineQualityReportMetadata();

            WriteHeader(builder, report, metadata);
            WriteSummary(builder, report);
            WriteFailureBreakdown(builder, report);
            WriteDetailedResults(builder, report);
            WriteMeasurementNotes(builder, report);
            WriteRecommendations(builder, report, metadata);

            return builder.ToString();
        }

        private static void WriteHeader(StringBuilder builder, LineQualityReport report,
            LineQualityReportMetadata metadata)
        {
            builder.AppendLine("# OSDP Line Quality Test Report");
            builder.AppendLine();
            builder.AppendLine("## Test Information");
            builder.AppendLine();
            builder.AppendLine("| Field | Value |");
            builder.AppendLine("|---|---|");

            AppendRow(builder, "Test Date",
                report.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            AppendRow(builder, "Tester Name", metadata.TesterName);
            AppendRow(builder, "Installation Location", metadata.InstallationLocation);
            AppendRow(builder, "ACU Model/Firmware", metadata.AcuDescription);
            AppendRow(builder, "PD Model/Firmware", metadata.PdDescription);
            AppendRow(builder, "Cable Type/Length", metadata.CableDescription);
            AppendRow(builder, "Connection", report.Connection);
            AppendRow(builder, "Responder Address",
                $"{report.Address} (0x{report.Address:X2})");
            AppendRow(builder, "Test Profile",
                $"{report.Profile} ({report.IterationsPerCombination} iterations per combination)");
            AppendRow(builder, "Duration", FormatDuration(report.Duration));
            AppendRow(builder, "Controller Platform / Adapter", metadata.AdapterDescription);
            AppendRow(builder, "Adapter Latency Timer Adjusted",
                metadata.AdapterLatencyTimerAdjusted.HasValue
                    ? metadata.AdapterLatencyTimerAdjusted.Value ? "Yes" : "No"
                    : null);

            builder.AppendLine();
        }

        private static void WriteSummary(StringBuilder builder, LineQualityReport report)
        {
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine($"**Overall result: {Describe(report.OverallVerdict)}**");
            builder.AppendLine();
            builder.AppendLine(report.RecommendedBaudRate.HasValue
                ? $"**Recommended operating baud rate: {report.RecommendedBaudRate.Value}**"
                : "**Recommended operating baud rate: none — no rate passed cleanly.**");
            builder.AppendLine();

            builder.AppendLine(
                "| Baud Rate | Sent | Failures | Success Rate | Detection Limit | Max Response (ms) | Result |");
            builder.AppendLine("|---:|---:|---:|---:|---:|---:|:---|");

            foreach (var result in report.BaudRates)
            {
                if (!result.WasTested)
                {
                    builder.AppendLine(
                        $"| {result.BaudRate} | — | — | — | — | — | {Describe(result.Verdict)} |");
                    continue;
                }

                builder.AppendLine(
                    $"| {result.BaudRate} | {result.PacketsSent} | {result.Failures} | " +
                    $"{Percent(result.SuccessRatePercent)} | {Percent(result.DetectionLimitPercent)} | " +
                    $"{result.MaxResponseTimeMs:F1} | {Describe(result.Verdict)} |");
            }

            builder.AppendLine();

            // The single most important caveat in the whole document: a pass bounds the loss rate,
            // it does not prove the absence of loss.
            builder.AppendLine(
                "> A **PASS** establishes only that the packet loss rate is below the detection " +
                "limit shown for that rate. It does not establish that loss is zero. The detection " +
                "limit is set by how many packets the profile sent (see Measurement Notes).");
            builder.AppendLine();

            WriteSkipped(builder, report);
        }

        private static void WriteSkipped(StringBuilder builder, LineQualityReport report)
        {
            var failed = new List<BaudRateResult>();
            var skipped = new List<BaudRateResult>();

            foreach (var result in report.BaudRates)
            {
                if (result.FailureReason != null) failed.Add(result);
                else if (result.SkipReason != null) skipped.Add(result);
            }

            if (failed.Count > 0)
            {
                builder.AppendLine("### Rate Changes That Did Not Complete");
                builder.AppendLine();
                foreach (var result in failed)
                {
                    builder.AppendLine($"- **{result.BaudRate}** — {result.FailureReason}");
                }

                builder.AppendLine();
                builder.AppendLine(
                    "> These count as failures of the rate rather than untested rates: the line " +
                    "could not carry the transition, or could not sustain the rate once there.");
                builder.AppendLine();
            }

            if (skipped.Count == 0) return;

            builder.AppendLine("### Rates Not Tested");
            builder.AppendLine();
            foreach (var result in skipped)
            {
                builder.AppendLine($"- **{result.BaudRate}** — {result.SkipReason}");
            }

            builder.AppendLine();
        }

        private static void WriteFailureBreakdown(StringBuilder builder, LineQualityReport report)
        {
            var withFailures = new List<BaudRateResult>();
            foreach (var result in report.BaudRates)
            {
                if (result.WasTested && result.Failures > 0) withFailures.Add(result);
            }

            if (withFailures.Count == 0) return;

            builder.AppendLine("## Failure Breakdown");
            builder.AppendLine();
            builder.AppendLine(
                "| Baud Rate | Timeouts | Integrity Errors | NAKs | Pattern Mismatches |");
            builder.AppendLine("|---:|---:|---:|---:|---:|");

            foreach (var result in withFailures)
            {
                builder.AppendLine(
                    $"| {result.BaudRate} | {result.Timeouts} | {result.IntegrityErrors} | " +
                    $"{result.Naks} | {result.PatternMismatches} |");
            }

            builder.AppendLine();
        }

        private static void WriteDetailedResults(StringBuilder builder, LineQualityReport report)
        {
            builder.AppendLine("## Detailed Results");
            builder.AppendLine();

            foreach (var result in report.BaudRates)
            {
                if (!result.WasTested) continue;

                builder.AppendLine($"### {result.BaudRate} baud — {Describe(result.Verdict)}");
                builder.AppendLine();
                builder.AppendLine(
                    "| Pattern | Size | Sent | Received | Timeouts | Integrity | NAKs | " +
                    "Mismatches | Success % | Avg ms | Max ms |");
                builder.AppendLine("|:---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

                foreach (var combination in result.Combinations)
                {
                    builder.AppendLine(
                        $"| {combination.Pattern} | {combination.PayloadLength} | " +
                        $"{combination.PacketsSent} | {combination.PacketsReceived} | " +
                        $"{combination.Timeouts} | {combination.IntegrityErrors} | " +
                        $"{combination.Naks} | {combination.PatternMismatches} | " +
                        $"{Percent(combination.SuccessRatePercent)} | " +
                        $"{combination.AverageResponseTimeMs:F1} | {combination.MaxResponseTimeMs:F1} |");
                }

                builder.AppendLine();
            }
        }

        private static void WriteMeasurementNotes(StringBuilder builder, LineQualityReport report)
        {
            builder.AppendLine("## Measurement Notes");
            builder.AppendLine();

            builder.AppendLine(
                $"- **Profile:** {report.Profile}, {report.IterationsPerCombination} iterations of each " +
                "of the 16 pattern and size combinations per baud rate.");
            builder.AppendLine(
                "- **Detection limit:** with no observed failures, the 95% upper bound on the loss " +
                "rate is approximately 3/N for N packets. Only the Extended profile (3200 packets " +
                "per rate) can substantiate a 99.9% success claim.");
            builder.AppendLine(
                "- **Response time** is measured from the end of the command to the first byte of " +
                "the reply, matching REPLY_DELAY in OSDP section 5.7.");
            builder.AppendLine(
                "- **Timing resolution on a PC is limited by the serial adapter**, not the cable. " +
                "FTDI parts default to a 16 ms latency timer, which at high baud rates exceeds the " +
                "entire exchange. Treat the 200 ms reply window as a pass/fail gate; treat average " +
                "and maximum times as indicative only unless the latency timer was lowered.");
            builder.AppendLine();

            builder.AppendLine("Failure categories point at different physical causes:");
            builder.AppendLine();
            builder.AppendLine("| Category | Usual cause |");
            builder.AppendLine("|:---|:---|");
            builder.AppendLine("| Timeouts | Open or unpowered line, wrong address, termination fault |");
            builder.AppendLine("| Integrity errors | Marginal signal integrity, EMI, reflections |");
            builder.AppendLine("| NAKs | Responder rejected the command; check the error code in the log |");
            builder.AppendLine("| Pattern mismatches | Bit error that survived the CRC, or a responder defect |");
            builder.AppendLine();
        }

        private static void WriteRecommendations(StringBuilder builder, LineQualityReport report,
            LineQualityReportMetadata metadata)
        {
            builder.AppendLine("## Recommendations");
            builder.AppendLine();

            if (report.RecommendedBaudRate.HasValue)
            {
                builder.AppendLine(
                    $"- **Recommended operating baud rate:** {report.RecommendedBaudRate.Value}");

                if (report.Profile != TestProfile.Extended)
                {
                    builder.AppendLine(
                        "- Re-run with `--profile extended` at the selected rate before recording " +
                        "this as a commissioning result.");
                }
            }
            else
            {
                builder.AppendLine(
                    "- **Recommended operating baud rate:** none. No rate passed cleanly; " +
                    "investigate the failures above before selecting a rate.");
            }

            builder.AppendLine("- **Installation quality assessment:**");
            builder.AppendLine("- **Remediation actions (if any):**");
            builder.AppendLine();

            if (string.IsNullOrWhiteSpace(metadata.Notes)) return;

            builder.AppendLine("### Notes");
            builder.AppendLine();
            builder.AppendLine(metadata.Notes);
            builder.AppendLine();
        }

        private static void AppendRow(StringBuilder builder, string field, string value) =>
            builder.AppendLine($"| {field} | {(string.IsNullOrWhiteSpace(value) ? string.Empty : value)} |");

        private static string Percent(double value) =>
            value.ToString("F2", CultureInfo.InvariantCulture) + "%";

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
