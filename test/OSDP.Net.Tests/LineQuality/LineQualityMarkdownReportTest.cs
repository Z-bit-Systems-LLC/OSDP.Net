using System;
using NUnit.Framework;
using OSDP.Net.LineQuality;

namespace OSDP.Net.Tests.LineQuality
{
    [TestFixture]
    public class LineQualityMarkdownReportTest
    {
        [Test]
        public void Render_RejectsANullReport()
        {
            Assert.Throws<ArgumentNullException>(() => LineQualityMarkdownReport.Render(null));
        }

        [Test]
        public void Render_IncludesEveryRequiredSection()
        {
            var markdown = LineQualityMarkdownReport.Render(BuildReport());

            Assert.Multiple(() =>
            {
                Assert.That(markdown, Does.StartWith("# OSDP Line Quality Test Report"));
                Assert.That(markdown, Does.Contain("## Test Information"));
                Assert.That(markdown, Does.Contain("## Summary"));
                Assert.That(markdown, Does.Contain("## Detailed Results"));
                Assert.That(markdown, Does.Contain("## Measurement Notes"));
                Assert.That(markdown, Does.Contain("## Recommendations"));
            });
        }

        [Test]
        public void Render_StatesThatAPassOnlyBoundsTheLossRate()
        {
            // The most misreadable number in the report is a 100% success rate. The caveat has to
            // travel with it.
            var markdown = LineQualityMarkdownReport.Render(BuildReport());

            Assert.Multiple(() =>
            {
                Assert.That(markdown, Does.Contain("does not establish that loss is zero"));
                Assert.That(markdown, Does.Contain("0.31%"));
            });
        }

        [Test]
        public void Render_LeavesUnknownHeaderFieldsBlankForTheTechnician()
        {
            var markdown = LineQualityMarkdownReport.Render(BuildReport());

            Assert.Multiple(() =>
            {
                Assert.That(markdown, Does.Contain("| Tester Name |  |"));
                Assert.That(markdown, Does.Contain("| Cable Type/Length |  |"));
            });
        }

        [Test]
        public void Render_FillsInSuppliedMetadata()
        {
            var metadata = new LineQualityReportMetadata
            {
                TesterName = "A Tech",
                CableDescription = "Belden 9841, 150m",
                AdapterLatencyTimerAdjusted = true,
                Notes = "Rerouted away from the motor room."
            };

            var markdown = LineQualityMarkdownReport.Render(BuildReport(), metadata);

            Assert.Multiple(() =>
            {
                Assert.That(markdown, Does.Contain("| Tester Name | A Tech |"));
                Assert.That(markdown, Does.Contain("| Cable Type/Length | Belden 9841, 150m |"));
                Assert.That(markdown, Does.Contain("| Adapter Latency Timer Adjusted | Yes |"));
                Assert.That(markdown, Does.Contain("Rerouted away from the motor room."));
            });
        }

        [Test]
        public void Render_OmitsTheFailureBreakdownWhenNothingFailed()
        {
            var markdown = LineQualityMarkdownReport.Render(BuildReport());

            Assert.That(markdown, Does.Not.Contain("## Failure Breakdown"));
        }

        [Test]
        public void Render_BreaksDownFailuresByCategory()
        {
            var report = new LineQualityReport(TestProfile.Screening, "COM3",
                LineQualityProtocol.TestAddress);
            var result = report.AddBaudRate(230400);
            var combination = result.AddCombination(TestPattern.AlternatingA, 113);
            combination.PacketsSent = 10;
            combination.PacketsReceived = 6;
            combination.Timeouts = 2;
            combination.IntegrityErrors = 1;
            combination.PatternMismatches = 1;
            combination.RecordResponseTime(5.0);
            report.CompletedUtc = report.StartedUtc.AddSeconds(30);

            var markdown = LineQualityMarkdownReport.Render(report);

            Assert.Multiple(() =>
            {
                Assert.That(markdown, Does.Contain("## Failure Breakdown"));
                Assert.That(markdown, Does.Contain("| 230400 | 2 | 1 | 0 | 1 |"));
                Assert.That(markdown, Does.Contain("FAIL"));
            });
        }

        [Test]
        public void Render_ExplainsWhyARateWasNotTested()
        {
            var report = new LineQualityReport(TestProfile.Screening, "COM3",
                LineQualityProtocol.TestAddress);
            report.AddBaudRate(230400).SkipReason = "The responder did not switch to 230400 baud.";
            report.CompletedUtc = report.StartedUtc.AddSeconds(5);

            var markdown = LineQualityMarkdownReport.Render(report);

            Assert.Multiple(() =>
            {
                Assert.That(markdown, Does.Contain("### Rates Not Tested"));
                Assert.That(markdown, Does.Contain("did not switch to 230400 baud"));
                Assert.That(markdown, Does.Contain("UNTESTED"));
            });
        }

        [Test]
        public void Render_SeparatesAFailedRateChangeFromAnUntestedRate()
        {
            var report = new LineQualityReport(TestProfile.Screening, "COM3",
                LineQualityProtocol.TestAddress);

            var passing = report.AddBaudRate(115200);
            var combination = passing.AddCombination(TestPattern.AllZeros, 48);
            combination.PacketsSent = 160;
            combination.PacketsReceived = 160;
            combination.RecordResponseTime(20.0);

            report.AddBaudRate(230400).FailureReason =
                "The responder acknowledged the change to 230400 baud but could not be reached.";
            report.AddBaudRate(460800).SkipReason =
                "The responder reported that it does not support 460800 baud.";
            report.CompletedUtc = report.StartedUtc.AddMinutes(1);

            var markdown = LineQualityMarkdownReport.Render(report);

            Assert.Multiple(() =>
            {
                Assert.That(markdown, Does.Contain("### Rate Changes That Did Not Complete"));
                Assert.That(markdown, Does.Contain("### Rates Not Tested"));
                Assert.That(markdown, Does.Contain("count as failures of the rate"));

                // 230400 failed, 460800 was merely untested.
                Assert.That(markdown, Does.Contain("| 230400 | — | — | — | — | — | FAIL |"));
                Assert.That(markdown, Does.Contain("| 460800 | — | — | — | — | — | UNTESTED |"));
            });
        }

        [Test]
        public void Render_TellsAScreeningRunToRepeatAtTheExtendedProfile()
        {
            var markdown = LineQualityMarkdownReport.Render(BuildReport());

            Assert.That(markdown, Does.Contain("--profile extended"));
        }

        [Test]
        public void Render_KeepsTheTimingCaveatWithTheTimingNumbers()
        {
            var markdown = LineQualityMarkdownReport.Render(BuildReport());

            Assert.That(markdown, Does.Contain("latency timer"));
        }

        private static LineQualityReport BuildReport()
        {
            var report = new LineQualityReport(TestProfile.Qualification, "COM3",
                LineQualityProtocol.TestAddress);

            var result = report.AddBaudRate(230400);
            var combination = result.AddCombination(TestPattern.Sequential, 113);
            combination.PacketsSent = 960;
            combination.PacketsReceived = 960;
            combination.RecordResponseTime(12.5);

            report.CompletedUtc = report.StartedUtc.AddSeconds(35);
            return report;
        }
    }
}
