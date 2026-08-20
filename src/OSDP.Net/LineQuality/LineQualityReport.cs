using System;
using System.Collections.Generic;
using System.Linq;

namespace OSDP.Net.LineQuality
{
    /// <summary>
    /// The overall assessment of a baud rate or of a whole test run.
    /// </summary>
    public enum LineQualityVerdict
    {
        /// <summary>The rate was not exercised, usually because the responder refused to switch to it.</summary>
        Untested,

        /// <summary>No failures of any kind, and every response comfortably inside the reply window.</summary>
        Pass,

        /// <summary>Usable, but with at least one failure or a response time approaching the limit.</summary>
        Marginal,

        /// <summary>Not usable at this rate.</summary>
        Fail
    }

    /// <summary>
    /// Results for a single pattern and payload size at one baud rate.
    /// </summary>
    public class CombinationResult
    {
        private readonly List<double> _responseTimesMs = new List<double>();

        internal CombinationResult(TestPattern pattern, int payloadLength)
        {
            Pattern = pattern;
            PayloadLength = payloadLength;
        }

        /// <summary>Gets the pattern exercised.</summary>
        public TestPattern Pattern { get; }

        /// <summary>Gets the payload length exercised.</summary>
        public int PayloadLength { get; }

        /// <summary>Gets the number of echo requests sent.</summary>
        public int PacketsSent { get; internal set; }

        /// <summary>Gets the number of correct echoes received.</summary>
        public int PacketsReceived { get; internal set; }

        /// <summary>Gets the number of requests that drew no reply within the timeout.</summary>
        public int Timeouts { get; internal set; }

        /// <summary>Gets the number of replies that arrived but failed their integrity check.</summary>
        public int IntegrityErrors { get; internal set; }

        /// <summary>Gets the number of replies that were a NAK.</summary>
        public int Naks { get; internal set; }

        /// <summary>
        /// Gets the number of replies that passed their integrity check but did not carry the
        /// expected sequence number, status, or pattern data.
        /// </summary>
        public int PatternMismatches { get; internal set; }

        /// <summary>Gets the total number of failures of any kind.</summary>
        public int Failures => Timeouts + IntegrityErrors + Naks + PatternMismatches;

        /// <summary>Gets the proportion of requests that produced a correct echo, as a percentage.</summary>
        public double SuccessRatePercent =>
            PacketsSent == 0 ? 0.0 : 100.0 * PacketsReceived / PacketsSent;

        /// <summary>Gets the shortest observed response time, or zero when nothing was received.</summary>
        public double MinResponseTimeMs => _responseTimesMs.Count == 0 ? 0.0 : _responseTimesMs.Min();

        /// <summary>Gets the longest observed response time, or zero when nothing was received.</summary>
        public double MaxResponseTimeMs => _responseTimesMs.Count == 0 ? 0.0 : _responseTimesMs.Max();

        /// <summary>Gets the mean observed response time, or zero when nothing was received.</summary>
        public double AverageResponseTimeMs => _responseTimesMs.Count == 0 ? 0.0 : _responseTimesMs.Average();

        internal void RecordResponseTime(double milliseconds) => _responseTimesMs.Add(milliseconds);
    }

    /// <summary>
    /// Results for every combination exercised at a single baud rate.
    /// </summary>
    public class BaudRateResult
    {
        private readonly List<CombinationResult> _combinations = new List<CombinationResult>();

        internal BaudRateResult(int baudRate)
        {
            BaudRate = baudRate;
        }

        /// <summary>Gets the baud rate these results describe.</summary>
        public int BaudRate { get; }

        /// <summary>Gets the per-combination results.</summary>
        public IReadOnlyList<CombinationResult> Combinations => _combinations;

        /// <summary>
        /// Gets the reason the rate was not exercised because the responder cannot do it, or null.
        /// </summary>
        /// <remarks>
        /// A responder limitation says nothing about the cable, so a rate skipped for this reason
        /// is <see cref="LineQualityVerdict.Untested"/> rather than a failure.
        /// </remarks>
        public string SkipReason { get; internal set; }

        /// <summary>
        /// Gets the reason the rate failed before any packets could be measured, or null.
        /// </summary>
        /// <remarks>
        /// Set when the baud rate change did not complete: the responder never acknowledged it, or
        /// acknowledged it and then could not be reached. Section 6.1 counts an incomplete rate
        /// change as a failure of that rate, because the line could not carry it.
        /// </remarks>
        public string FailureReason { get; internal set; }

        /// <summary>Gets a value indicating whether any packets were sent at this rate.</summary>
        public bool WasTested => SkipReason == null && FailureReason == null && PacketsSent > 0;

        /// <summary>Gets the total number of echo requests sent at this rate.</summary>
        public int PacketsSent => _combinations.Sum(combination => combination.PacketsSent);

        /// <summary>Gets the total number of correct echoes received at this rate.</summary>
        public int PacketsReceived => _combinations.Sum(combination => combination.PacketsReceived);

        /// <summary>Gets the total number of timeouts at this rate.</summary>
        public int Timeouts => _combinations.Sum(combination => combination.Timeouts);

        /// <summary>Gets the total number of integrity failures at this rate.</summary>
        public int IntegrityErrors => _combinations.Sum(combination => combination.IntegrityErrors);

        /// <summary>Gets the total number of NAK replies at this rate.</summary>
        public int Naks => _combinations.Sum(combination => combination.Naks);

        /// <summary>Gets the total number of pattern mismatches at this rate.</summary>
        public int PatternMismatches => _combinations.Sum(combination => combination.PatternMismatches);

        /// <summary>Gets the total number of failures of any kind at this rate.</summary>
        public int Failures => Timeouts + IntegrityErrors + Naks + PatternMismatches;

        /// <summary>Gets the proportion of requests that produced a correct echo, as a percentage.</summary>
        public double SuccessRatePercent =>
            PacketsSent == 0 ? 0.0 : 100.0 * PacketsReceived / PacketsSent;

        /// <summary>Gets the longest response time observed at this rate, in milliseconds.</summary>
        public double MaxResponseTimeMs =>
            _combinations.Count == 0 ? 0.0 : _combinations.Max(combination => combination.MaxResponseTimeMs);

        /// <summary>Gets the mean response time across every successful exchange at this rate.</summary>
        public double AverageResponseTimeMs
        {
            get
            {
                int received = PacketsReceived;
                if (received == 0) return 0.0;

                double weighted = _combinations.Sum(combination =>
                    combination.AverageResponseTimeMs * combination.PacketsReceived);
                return weighted / received;
            }
        }

        /// <summary>
        /// Gets the smallest packet loss rate this run could have ruled out, as a percentage.
        /// A verdict of <see cref="LineQualityVerdict.Pass"/> means only that loss is below this
        /// figure, not that it is zero.
        /// </summary>
        public double DetectionLimitPercent => LineQualityProtocol.DetectionLimitPercent(PacketsSent);

        /// <summary>
        /// Gets the assessment for this baud rate, per section 6.1 of the test procedure.
        /// </summary>
        /// <remarks>
        /// The bands overlap, so they are applied worst-first: a run that would qualify as a pass
        /// on failure count can still be marginal on timing.
        /// </remarks>
        public LineQualityVerdict Verdict
        {
            get
            {
                // An incomplete rate change is a failure of the rate, even though it produced no
                // packets to count. Reporting it as merely "untested" would hide the finding.
                if (FailureReason != null) return LineQualityVerdict.Fail;

                if (!WasTested) return LineQualityVerdict.Untested;

                if (SuccessRatePercent < 99.0 || MaxResponseTimeMs > MaximumReplyDelayMs)
                {
                    return LineQualityVerdict.Fail;
                }

                if (Failures > 0 || MaxResponseTimeMs > MarginalReplyDelayMs)
                {
                    return LineQualityVerdict.Marginal;
                }

                return LineQualityVerdict.Pass;
            }
        }

        /// <summary>The reply window from OSDP section 5.7, in milliseconds.</summary>
        internal const double MaximumReplyDelayMs = 200.0;

        /// <summary>The point at which response times are close enough to the limit to warn.</summary>
        internal const double MarginalReplyDelayMs = 150.0;

        internal CombinationResult AddCombination(TestPattern pattern, int payloadLength)
        {
            var combination = new CombinationResult(pattern, payloadLength);
            _combinations.Add(combination);
            return combination;
        }
    }

    /// <summary>
    /// The complete results of a line quality test run.
    /// </summary>
    public class LineQualityReport
    {
        private readonly List<BaudRateResult> _baudRates = new List<BaudRateResult>();

        internal LineQualityReport(TestProfile profile, string connection, byte address)
        {
            Profile = profile;
            Connection = connection;
            Address = address;
            StartedUtc = DateTime.UtcNow;
        }

        /// <summary>Gets the profile the run used.</summary>
        public TestProfile Profile { get; }

        /// <summary>Gets a description of the connection tested.</summary>
        public string Connection { get; }

        /// <summary>Gets the responder address that was tested.</summary>
        public byte Address { get; }

        /// <summary>Gets when the run started.</summary>
        public DateTime StartedUtc { get; }

        /// <summary>Gets when the run finished.</summary>
        public DateTime CompletedUtc { get; internal set; }

        /// <summary>Gets how long the run took.</summary>
        public TimeSpan Duration => CompletedUtc - StartedUtc;

        /// <summary>Gets the per-baud-rate results, in the order they were exercised.</summary>
        public IReadOnlyList<BaudRateResult> BaudRates => _baudRates;

        /// <summary>
        /// Gets the number of iterations run for each pattern and payload combination.
        /// </summary>
        public int IterationsPerCombination => LineQualityProtocol.IterationsPerCombination(Profile);

        /// <summary>
        /// Gets the highest baud rate that passed, or null when none did.
        /// </summary>
        /// <remarks>
        /// Only a pass counts. A marginal result means the line is carrying errors already, which
        /// is not a basis for choosing an operating rate.
        /// </remarks>
        public int? RecommendedBaudRate => _baudRates
            .Where(result => result.Verdict == LineQualityVerdict.Pass)
            .Select(result => (int?)result.BaudRate)
            .DefaultIfEmpty(null)
            .Max();

        /// <summary>
        /// Gets the overall verdict: the best result achieved at any rate.
        /// </summary>
        public LineQualityVerdict OverallVerdict
        {
            get
            {
                // A rate that failed its transition counts here even though it produced no packets.
                var assessed = _baudRates
                    .Where(result => result.Verdict != LineQualityVerdict.Untested)
                    .ToArray();

                if (assessed.Length == 0) return LineQualityVerdict.Untested;

                if (assessed.Any(result => result.Verdict == LineQualityVerdict.Pass))
                {
                    return LineQualityVerdict.Pass;
                }

                return assessed.Any(result => result.Verdict == LineQualityVerdict.Marginal)
                    ? LineQualityVerdict.Marginal
                    : LineQualityVerdict.Fail;
            }
        }

        internal BaudRateResult AddBaudRate(int baudRate)
        {
            var result = new BaudRateResult(baudRate);
            _baudRates.Add(result);
            return result;
        }
    }
}
