using NUnit.Framework;
using OSDP.Net.LineQuality;

namespace OSDP.Net.Tests.LineQuality
{
    /// <summary>
    /// Exercises the verdict rules from section 6.1 of the test procedure. These decide what a
    /// technician is told about an installation, so they are worth pinning down without hardware.
    /// </summary>
    [TestFixture]
    public class LineQualityReportTest
    {
        [Test]
        public void CleanRun_Passes()
        {
            var result = BuildResult(sent: 960, received: 960, maxResponseMs: 4.0);

            Assert.Multiple(() =>
            {
                Assert.That(result.Verdict, Is.EqualTo(LineQualityVerdict.Pass));
                Assert.That(result.Failures, Is.Zero);
                Assert.That(result.SuccessRatePercent, Is.EqualTo(100.0));
            });
        }

        [Test]
        public void SingleTimeout_IsMarginalNotAPass()
        {
            var result = BuildResult(sent: 960, received: 959, maxResponseMs: 4.0, timeouts: 1);

            Assert.Multiple(() =>
            {
                Assert.That(result.Verdict, Is.EqualTo(LineQualityVerdict.Marginal));
                Assert.That(result.SuccessRatePercent, Is.EqualTo(99.9).Within(0.01));
            });
        }

        [Test]
        public void SuccessRateBelowNinetyNinePercent_Fails()
        {
            var result = BuildResult(sent: 100, received: 98, maxResponseMs: 4.0, timeouts: 2);

            Assert.That(result.Verdict, Is.EqualTo(LineQualityVerdict.Fail));
        }

        [Test]
        public void ResponseBeyondTheReplyWindow_FailsEvenWithNoLostPackets()
        {
            // OSDP section 5.7 caps REPLY_DELAY at 200 ms. A responder that answers late is out of
            // spec regardless of how reliably it answers.
            var result = BuildResult(sent: 960, received: 960, maxResponseMs: 250.0);

            Assert.That(result.Verdict, Is.EqualTo(LineQualityVerdict.Fail));
        }

        [Test]
        public void ResponseApproachingTheReplyWindow_IsMarginal()
        {
            var result = BuildResult(sent: 960, received: 960, maxResponseMs: 175.0);

            Assert.That(result.Verdict, Is.EqualTo(LineQualityVerdict.Marginal));
        }

        [Test]
        public void SkippedRate_IsUntestedRatherThanFailed()
        {
            var report = new LineQualityReport(TestProfile.Screening, "COM3", LineQualityProtocol.TestAddress);
            var result = report.AddBaudRate(230400);
            result.SkipReason = "The responder did not switch to 230400 baud.";

            Assert.Multiple(() =>
            {
                Assert.That(result.Verdict, Is.EqualTo(LineQualityVerdict.Untested));
                Assert.That(result.WasTested, Is.False);
            });
        }

        [Test]
        public void IncompleteRateChange_IsAFailureNotAnUntestedRate()
        {
            // Section 6.1 counts a rate change that did not complete as a failure of that rate.
            // Reporting it as untested would hide a line that cannot carry the transition.
            var report = new LineQualityReport(TestProfile.Screening, "COM3",
                LineQualityProtocol.TestAddress);
            var result = report.AddBaudRate(230400);
            result.FailureReason = "acknowledged but unreachable at that rate";

            Assert.Multiple(() =>
            {
                Assert.That(result.Verdict, Is.EqualTo(LineQualityVerdict.Fail));
                Assert.That(result.WasTested, Is.False);
                Assert.That(result.PacketsSent, Is.Zero);
            });
        }

        [Test]
        public void ResponderThatCannotDoARate_LeavesItUntested()
        {
            // A device limitation says nothing about the cable.
            var report = new LineQualityReport(TestProfile.Screening, "COM3",
                LineQualityProtocol.TestAddress);
            var result = report.AddBaudRate(460800);
            result.SkipReason = "The responder reported that it does not support 460800 baud.";

            Assert.That(result.Verdict, Is.EqualTo(LineQualityVerdict.Untested));
        }

        [Test]
        public void FailedRateChange_CountsTowardTheOverallVerdict()
        {
            var report = new LineQualityReport(TestProfile.Screening, "COM3",
                LineQualityProtocol.TestAddress);
            report.AddBaudRate(230400).FailureReason = "acknowledged but unreachable at that rate";

            Assert.Multiple(() =>
            {
                Assert.That(report.OverallVerdict, Is.EqualTo(LineQualityVerdict.Fail));
                Assert.That(report.RecommendedBaudRate, Is.Null);
            });
        }

        [Test]
        public void ErrorCategories_AreCountedSeparately()
        {
            var report = new LineQualityReport(TestProfile.Screening, "COM3", LineQualityProtocol.TestAddress);
            var result = report.AddBaudRate(9600);
            var combination = result.AddCombination(TestPattern.AllZeros, 48);

            combination.PacketsSent = 10;
            combination.PacketsReceived = 6;
            combination.Timeouts = 1;
            combination.IntegrityErrors = 1;
            combination.Naks = 1;
            combination.PatternMismatches = 1;

            Assert.Multiple(() =>
            {
                Assert.That(result.Timeouts, Is.EqualTo(1));
                Assert.That(result.IntegrityErrors, Is.EqualTo(1));
                Assert.That(result.Naks, Is.EqualTo(1));
                Assert.That(result.PatternMismatches, Is.EqualTo(1));
                Assert.That(result.Failures, Is.EqualTo(4));
            });
        }

        [Test]
        public void DetectionLimit_ReflectsHowManyPacketsWereActuallySent()
        {
            var screening = BuildResult(sent: 160, received: 160, maxResponseMs: 3.0);
            var qualification = BuildResult(sent: 960, received: 960, maxResponseMs: 3.0);

            Assert.Multiple(() =>
            {
                Assert.That(screening.DetectionLimitPercent, Is.EqualTo(1.875).Within(0.001));
                Assert.That(qualification.DetectionLimitPercent, Is.EqualTo(0.3125).Within(0.001));
            });
        }

        [Test]
        public void RecommendedBaudRate_IsTheHighestThatPassed()
        {
            var report = new LineQualityReport(TestProfile.Qualification, "COM3",
                LineQualityProtocol.TestAddress);

            AddResult(report, 9600, sent: 960, received: 960, maxResponseMs: 3.0);
            AddResult(report, 115200, sent: 960, received: 960, maxResponseMs: 3.0);
            AddResult(report, 230400, sent: 960, received: 950, maxResponseMs: 3.0, timeouts: 10);

            Assert.Multiple(() =>
            {
                Assert.That(report.RecommendedBaudRate, Is.EqualTo(115200));
                Assert.That(report.OverallVerdict, Is.EqualTo(LineQualityVerdict.Pass));
            });
        }

        [Test]
        public void RecommendedBaudRate_IgnoresMarginalRates()
        {
            // A marginal rate is already carrying errors, which is not a basis for choosing an
            // operating rate even when it is the only rate that responded.
            var report = new LineQualityReport(TestProfile.Qualification, "COM3",
                LineQualityProtocol.TestAddress);

            AddResult(report, 9600, sent: 960, received: 959, maxResponseMs: 3.0, timeouts: 1);

            Assert.Multiple(() =>
            {
                Assert.That(report.RecommendedBaudRate, Is.Null);
                Assert.That(report.OverallVerdict, Is.EqualTo(LineQualityVerdict.Marginal));
            });
        }

        [Test]
        public void ReportWithNothingTested_IsUntested()
        {
            var report = new LineQualityReport(TestProfile.Screening, "COM3", LineQualityProtocol.TestAddress);
            report.AddBaudRate(9600).SkipReason = "no responder";

            Assert.That(report.OverallVerdict, Is.EqualTo(LineQualityVerdict.Untested));
        }

        private static BaudRateResult BuildResult(int sent, int received, double maxResponseMs,
            int timeouts = 0)
        {
            var report = new LineQualityReport(TestProfile.Qualification, "COM3",
                LineQualityProtocol.TestAddress);
            return AddResult(report, 9600, sent, received, maxResponseMs, timeouts);
        }

        private static BaudRateResult AddResult(LineQualityReport report, int baudRate, int sent,
            int received, double maxResponseMs, int timeouts = 0)
        {
            var result = report.AddBaudRate(baudRate);
            var combination = result.AddCombination(TestPattern.AllZeros, 48);

            combination.PacketsSent = sent;
            combination.PacketsReceived = received;
            combination.Timeouts = timeouts;

            if (received > 0)
            {
                combination.RecordResponseTime(maxResponseMs);
            }

            return result;
        }
    }
}
