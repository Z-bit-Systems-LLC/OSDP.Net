using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OSDP.Net.Connections;
using OSDP.Net.Messages;
using OSDP.Net.Model;
using OSDP.Net.Tracing;

namespace OSDP.Net.LineQuality
{
    /// <summary>
    /// The controller side of the OSDP Line Quality Test Procedure: sweeps baud rates, bounces
    /// test patterns off a responder, and reports what survived.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This drives the connection directly rather than going through <see cref="ControlPanel"/>.
    /// The polling bus queues commands and dispatches them on its own schedule, so round-trip
    /// times measured through it describe the scheduler rather than the line; it folds integrity
    /// failures in with timeouts, which erases the most diagnostic signal the test produces; and
    /// it retries on timeout, which would count a lost packet as delivered. None of that is wrong
    /// for normal operation, and all of it is fatal to a measurement.
    /// </para>
    /// <para>
    /// Response times are measured to the first byte of the reply, per OSDP section 5.7. Note that
    /// on a PC with a USB-to-RS-485 adapter the resolution is limited by the adapter's latency
    /// timer, commonly 16 ms; treat timing as a pass/fail gate against the 200 ms reply window
    /// rather than as a precise measurement.
    /// </para>
    /// </remarks>
    public sealed class LineQualityTest
    {
        /// <summary>
        /// How many times a presence probe is attempted before concluding nothing is there.
        /// </summary>
        private const int ProbeAttempts = 3;

        private readonly IRetunableOsdpConnection _connection;
        private readonly ILogger _logger;
        private readonly Guid _traceId = Guid.NewGuid();

        private LineQualityOptions _options;
        private byte _controlSequence = 1;
        private byte _testSequence;

        /// <summary>
        /// Initializes a line quality test controller.
        /// </summary>
        /// <param name="connection">The serial connection to test on. The test takes over the
        /// connection and retunes it as it sweeps baud rates.</param>
        /// <param name="loggerFactory">Optional logger factory.</param>
        /// <exception cref="ArgumentNullException">The connection is null.</exception>
        public LineQualityTest(IRetunableOsdpConnection connection, ILoggerFactory loggerFactory = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _logger = loggerFactory?.CreateLogger<LineQualityTest>();
        }

        /// <summary>
        /// Runs a line quality test.
        /// </summary>
        /// <param name="options">Options controlling the run, or null for defaults.</param>
        /// <param name="token">Token used to abandon the run.</param>
        /// <returns>The results.</returns>
        /// <exception cref="LineQualityException">No responder answered at any baud rate.</exception>
        public async Task<LineQualityReport> RunAsync(LineQualityOptions options = null,
            CancellationToken token = default)
        {
            _options = options ?? new LineQualityOptions();
            var baudRates = (_options.BaudRates ?? LineQualityProtocol.DefaultBaudRates)
                .Distinct().OrderBy(rate => rate).ToArray();

            if (baudRates.Length == 0)
            {
                throw new LineQualityException("No baud rates were selected for testing.");
            }

            var report = new LineQualityReport(_options.Profile, _connection.ToString(), _options.Address);

            if (!_connection.IsOpen)
            {
                await _connection.Open().ConfigureAwait(false);
            }

            _connection.ReplyTimeout = _options.ResponseTimeout;
            int baselineRate = LineQualityProtocol.ToBaudRate(LineQualityBaudRate.Baud9600);

            try
            {
                await EstablishContact(baselineRate, baudRates, token).ConfigureAwait(false);

                int completed = 0;
                foreach (int baudRate in baudRates)
                {
                    token.ThrowIfCancellationRequested();

                    var result = report.AddBaudRate(baudRate);
                    var outcome = await SwitchToBaudRate(baudRate, baudRates, token).ConfigureAwait(false);

                    if (outcome.Succeeded)
                    {
                        await RunMatrix(result, completed, baudRates.Length, token).ConfigureAwait(false);
                    }
                    else if (outcome.IsResponderLimitation)
                    {
                        // The responder told us it cannot do this rate. That is a property of the
                        // device, not of the cable, so it is not a line quality failure.
                        result.SkipReason = outcome.Reason;
                        _logger?.LogWarning("Skipping {BaudRate} baud: {Reason}", baudRate, outcome.Reason);
                    }
                    else
                    {
                        // The rate change did not complete. Section 6.1 counts that as a failure of
                        // the rate: the line could not carry the transition or could not sustain
                        // the new rate once there.
                        result.FailureReason = outcome.Reason;
                        _logger?.LogWarning("{BaudRate} baud failed: {Reason}", baudRate, outcome.Reason);
                    }

                    completed++;
                }
            }
            finally
            {
                if (_options.ReturnToBaselineWhenDone && _connection.BaudRate != baselineRate)
                {
                    await TryReturnToBaseline(baselineRate).ConfigureAwait(false);
                }

                report.CompletedUtc = DateTime.UtcNow;
            }

            return report;
        }

        /// <summary>
        /// Confirms a responder is present before any measurement starts, per section 4.1 step 4.
        /// </summary>
        private async Task EstablishContact(int baselineRate, IReadOnlyList<int> candidateRates,
            CancellationToken token)
        {
            Report("Looking for a responder", _connection.BaudRate, 0, 0, 0, candidateRates.Count);

            if (_connection.BaudRate != baselineRate)
            {
                _connection.SetBaudRate(baselineRate);
            }

            if (await ProbeAsync(token).ConfigureAwait(false)) return;

            _logger?.LogWarning("No responder at {BaudRate} baud; searching other rates", baselineRate);

            if (await RecoverBaudRate(candidateRates, token).ConfigureAwait(false)) return;

            throw new LineQualityException(
                $"No line quality responder answered at address {_options.Address} on {_connection} " +
                "at any baud rate. Check wiring, responder power, and that the responder is " +
                "configured for this address.");
        }

        /// <summary>
        /// Moves both ends to a baud rate, recovering if the responder does not arrive there.
        /// </summary>
        /// <remarks>
        /// The distinction in the return value matters for the verdict. A responder that answers
        /// "I do not support that rate" is describing itself, and the rate is simply untested. A
        /// rate change that goes unanswered, or that leaves the responder unreachable, is the line
        /// failing to carry the transition — section 6.1 counts that as a failure of the rate.
        /// </remarks>
        private async Task<SwitchOutcome> SwitchToBaudRate(int baudRate,
            IReadOnlyList<int> candidateRates, CancellationToken token)
        {
            if (_connection.BaudRate == baudRate) return SwitchOutcome.Success();

            if (!LineQualityProtocol.TryGetBaudRateId(baudRate, out var baudRateId))
            {
                return SwitchOutcome.NotSupported(
                    $"{baudRate} baud has no line quality baud rate ID.");
            }

            int currentRate = _connection.BaudRate;
            var status = await RequestBaudRateChange(baudRateId, token).ConfigureAwait(false);

            if (status == BaudRateChangeStatus.UnsupportedRate)
            {
                return SwitchOutcome.NotSupported(
                    $"The responder reported that it does not support {baudRate} baud.");
            }

            if (status == null)
            {
                // The responder may still have switched: an acknowledgment that was sent but lost
                // leaves the two ends on different rates. Search before giving up, so the failure
                // is recorded against this rate only and the next one starts from a rate that works.
                await RecoverBaudRate(candidateRates, token).ConfigureAwait(false);

                return SwitchOutcome.Failed(
                    $"The responder did not acknowledge the change to {baudRate} baud, sent at " +
                    $"{currentRate} baud.");
            }

            if (status != BaudRateChangeStatus.Success)
            {
                return SwitchOutcome.Failed(
                    $"The responder reported {status} when asked to switch to {baudRate} baud.");
            }

            _connection.SetBaudRate(baudRate);

            // Section 4.2 step 6: prove the new rate works before measuring anything at it.
            if (await ProbeAsync(token).ConfigureAwait(false)) return SwitchOutcome.Success();

            _logger?.LogWarning("No response after switching to {BaudRate} baud; attempting recovery", baudRate);
            await RecoverBaudRate(candidateRates, token).ConfigureAwait(false);

            return SwitchOutcome.Failed(
                $"The responder acknowledged the change to {baudRate} baud but could not be " +
                "reached at that rate, so the line will not sustain it.");
        }

        /// <summary>
        /// The result of trying to move both ends to a baud rate.
        /// </summary>
        private readonly struct SwitchOutcome
        {
            private SwitchOutcome(bool succeeded, bool isResponderLimitation, string reason)
            {
                Succeeded = succeeded;
                IsResponderLimitation = isResponderLimitation;
                Reason = reason;
            }

            public bool Succeeded { get; }

            /// <summary>
            /// True when the responder itself cannot do the rate, which says nothing about the line.
            /// </summary>
            public bool IsResponderLimitation { get; }

            public string Reason { get; }

            public static SwitchOutcome Success() => new SwitchOutcome(true, false, null);

            public static SwitchOutcome NotSupported(string reason) =>
                new SwitchOutcome(false, true, reason);

            public static SwitchOutcome Failed(string reason) =>
                new SwitchOutcome(false, false, reason);
        }

        private async Task<BaudRateChangeStatus?> RequestBaudRateChange(LineQualityBaudRate baudRateId,
            CancellationToken token)
        {
            var command = new BaudRateChange(NextTestSequence(), baudRateId);
            var exchange = await Exchange(command, token).ConfigureAwait(false);

            var reply = exchange.Reply;
            if (exchange.Outcome != ExchangeOutcome.Received || reply == null) return null;
            if (!reply.IsDataCorrect || reply.Address != _options.Address) return null;
            if (reply.Type != (byte)ReplyType.ManufactureSpecific) return null;
            if (!BaudRateChangeAck.TryParse(reply.Payload.AsSpan(), out var ack)) return null;
            if (ack.SequenceNumber != command.SequenceNumber) return null;

            if (ack.Status != BaudRateChangeStatus.Success)
            {
                _logger?.LogWarning("Responder rejected baud rate {BaudRateId} with status {Status}",
                    baudRateId, ack.Status);
                return ack.Status;
            }

            // The acknowledgment went out at the old rate; let it clear the line before either end
            // retunes, then observe the settle delay both ends agreed on.
            await _connection.WaitForTransmitCompleteAsync(exchange.CommandLength, token).ConfigureAwait(false);
            await Task.Delay(_options.BaudRateSettleDelay, token).ConfigureAwait(false);

            return BaudRateChangeStatus.Success;
        }

        /// <summary>
        /// Cycles through candidate rates looking for the responder, per section 4.4.
        /// </summary>
        private async Task<bool> RecoverBaudRate(IReadOnlyList<int> candidateRates, CancellationToken token)
        {
            var searchOrder = new List<int> { LineQualityProtocol.ToBaudRate(LineQualityBaudRate.Baud9600) };
            searchOrder.AddRange(candidateRates.OrderBy(rate => rate));

            foreach (int rate in searchOrder.Distinct())
            {
                token.ThrowIfCancellationRequested();

                _connection.SetBaudRate(rate);
                Report($"Recovering: probing {rate} baud", rate, 0, 0, 0, candidateRates.Count);

                // Two attempts per rate. One is too few: a single missed reply during the search
                // makes the controller walk past the rate the responder is actually on and give up
                // entirely, which is a far worse outcome than the extra 200 ms per rate costs.
                if (!await ProbeAsync(token, attempts: 2).ConfigureAwait(false)) continue;

                _logger?.LogInformation("Recovered contact at {BaudRate} baud", rate);
                return true;
            }

            // Nothing answered. Leaving the port on the last rate tried — the highest one — would
            // send every following rate change over a line that has just proven it cannot carry
            // that rate, turning one real failure into a cascade of invented ones. Fall back to the
            // baseline, which is also where a responder implementing the idle auto-revert will be.
            int baselineRate = LineQualityProtocol.ToBaudRate(LineQualityBaudRate.Baud9600);
            _connection.SetBaudRate(baselineRate);

            _logger?.LogWarning(
                "No responder found at any rate; returning the controller to {BaudRate} baud so " +
                "later rates are attempted from a known starting point", baselineRate);

            return false;
        }

        /// <summary>
        /// Puts the responder back on the baseline rate so the installation is left in a known
        /// state.
        /// </summary>
        /// <remarks>
        /// Retried, and the outcome checked: an unacknowledged change leaves the two ends on
        /// different rates, and silently retuning only this end turns that into a mystery for
        /// whoever runs the next test. A responder implementing the idle auto-revert recovers by
        /// itself, so the warning says so rather than implying the line is broken.
        /// </remarks>
        private async Task TryReturnToBaseline(int baselineRate)
        {
            if (!LineQualityProtocol.TryGetBaudRateId(baselineRate, out var baselineId)) return;

            for (int attempt = 0; attempt < ProbeAttempts; attempt++)
            {
                try
                {
                    var status = await RequestBaudRateChange(baselineId, CancellationToken.None)
                        .ConfigureAwait(false);

                    if (status == BaudRateChangeStatus.Success)
                    {
                        _connection.SetBaudRate(baselineRate);
                        return;
                    }
                }
                catch (Exception exception)
                {
                    _logger?.LogDebug(exception, "Baud rate reset attempt {Attempt} failed", attempt + 1);
                }
            }

            _logger?.LogWarning(
                "Could not confirm the responder returned to {BaudRate} baud; it may still be at " +
                "{CurrentRate} until its idle timeout returns it to the baseline",
                baselineRate, _connection.BaudRate);

            _connection.SetBaudRate(baselineRate);
        }

        /// <summary>
        /// Sends minimal echo requests to see whether a responder is answering.
        /// </summary>
        /// <remarks>
        /// Unlike a measured exchange, a probe may be retried. It establishes presence rather than
        /// contributing to any statistic, and the no-retry rule exists to stop retries from hiding
        /// packet loss in the results. Retrying matters in practice because the first exchange of a
        /// session is the slowest one: a managed responder is still warming up its reply path and
        /// can miss the 200 ms window once, which would otherwise send the controller into an
        /// unnecessary baud rate search.
        /// </remarks>
        private async Task<bool> ProbeAsync(CancellationToken token, int attempts = ProbeAttempts)
        {
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var request = new EchoRequest(NextTestSequence(), TestPattern.AlternatingA, 0);
                var exchange = await Exchange(request, token).ConfigureAwait(false);

                if (Classify(exchange, request, 0) == ExchangeOutcome.Received) return true;
            }

            return false;
        }

        private async Task RunMatrix(BaudRateResult result, int completedRates, int totalRates,
            CancellationToken token)
        {
            int iterations = LineQualityProtocol.IterationsPerCombination(_options.Profile);
            var combinations = BuildMatrix();
            int totalPackets = combinations.Count * iterations;
            int sent = 0;

            foreach (var combination in combinations)
            {
                var combinationResult = result.AddCombination(combination.Pattern, combination.PayloadLength);

                for (int iteration = 0; iteration < iterations; iteration++)
                {
                    token.ThrowIfCancellationRequested();

                    var request = new EchoRequest(NextTestSequence(), combination.Pattern,
                        combination.PayloadLength);
                    var exchange = await Exchange(request, token).ConfigureAwait(false);
                    var outcome = Classify(exchange, request, combination.PayloadLength);

                    combinationResult.PacketsSent++;
                    switch (outcome)
                    {
                        case ExchangeOutcome.Received:
                            combinationResult.PacketsReceived++;
                            combinationResult.RecordResponseTime(exchange.ResponseTimeMs);
                            break;
                        case ExchangeOutcome.Timeout:
                            combinationResult.Timeouts++;
                            break;
                        case ExchangeOutcome.IntegrityError:
                            combinationResult.IntegrityErrors++;
                            break;
                        case ExchangeOutcome.Nak:
                            combinationResult.Naks++;
                            break;
                        default:
                            combinationResult.PatternMismatches++;
                            break;
                    }

                    sent++;
                    if (sent % 10 == 0 || sent == totalPackets)
                    {
                        Report($"{result.BaudRate} baud", result.BaudRate, sent, totalPackets,
                            completedRates, totalRates);
                    }

                    // Section 5.7 requires an idle interval of two character times between packets.
                    await Task.Delay(LineQualityProtocol.TransmissionTime(2, _connection.BaudRate), token)
                        .ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Builds the 16 pattern and size combinations from section 3.10. The zero-length case is
        /// only meaningful for the four constant patterns; sequential and walking-one produce no
        /// bytes at all at that length.
        /// </summary>
        private static IReadOnlyList<MatrixEntry> BuildMatrix()
        {
            var entries = new List<MatrixEntry>();

            foreach (TestPattern pattern in new[]
                     {
                         TestPattern.AllZeros, TestPattern.AllOnes,
                         TestPattern.AlternatingA, TestPattern.Alternating5
                     })
            {
                foreach (int length in LineQualityProtocol.TestPayloadLengths)
                {
                    entries.Add(new MatrixEntry(pattern, length));
                }
            }

            foreach (TestPattern pattern in new[] { TestPattern.Sequential, TestPattern.WalkingOne })
            {
                foreach (int length in LineQualityProtocol.TestPayloadLengths.Where(length => length > 0))
                {
                    entries.Add(new MatrixEntry(pattern, length));
                }
            }

            return entries;
        }

        /// <summary>
        /// Sends one command and reads whatever comes back, timing the reply.
        /// </summary>
        /// <remarks>
        /// Deliberately never retries. A retry that succeeds would be counted as a delivery and
        /// would hide the loss this test exists to measure.
        /// </remarks>
        private async Task<ExchangeRecord> Exchange(PayloadData command, CancellationToken token)
        {
            var control = new Control(_controlSequence, true, false);
            var message = new OutgoingMessage(_options.Address, control, command)
                .BuildMessage(ClearTextChannel.Instance);

            AdvanceControlSequence();

            var stopwatch = Stopwatch.StartNew();
            await _connection.WriteAsync(message).ConfigureAwait(false);
            Trace(TraceDirection.Output, message);

            // OSDP section 5.7 measures the reply window from the last character of the command.
            // A write returns once the bytes are accepted for transmission, not once they have
            // left the wire, so subtract the frame's own transmission time from what we observe.
            var commandWireTime = LineQualityProtocol.TransmissionTime(message.Length, _connection.BaudRate);
            var deadline = commandWireTime + _options.ResponseTimeout;

            var buffer = new Collection<byte>();
            var firstByteAt = await WaitForReplyStart(buffer, stopwatch, deadline, token).ConfigureAwait(false);

            if (firstByteAt == null)
            {
                return ExchangeRecord.TimedOut(message.Length);
            }

            double responseTimeMs = Math.Max(0.0, firstByteAt.Value.TotalMilliseconds -
                                                  commandWireTime.TotalMilliseconds);

            if (!await Bus.WaitForMessageLength(_connection, buffer, token).ConfigureAwait(false) ||
                !await Bus.WaitForRestOfMessage(_connection, buffer, Bus.ExtractMessageLength(buffer), token)
                    .ConfigureAwait(false))
            {
                // A frame that started but never finished is a corrupted frame, not a silent line.
                return ExchangeRecord.Truncated(message.Length, responseTimeMs);
            }

            var raw = buffer.ToArray();
            Trace(TraceDirection.Input, raw);

            try
            {
                return ExchangeRecord.Replied(message.Length, responseTimeMs,
                    new IncomingMessage(raw, ClearTextChannel.Instance));
            }
            catch (Exception exception)
            {
                _logger?.LogDebug(exception, "Could not parse a reply frame");
                return ExchangeRecord.Truncated(message.Length, responseTimeMs);
            }
        }

        /// <summary>
        /// Waits for the reply to start, returning when it arrived or null on timeout.
        /// </summary>
        private async Task<TimeSpan?> WaitForReplyStart(ICollection<byte> buffer, Stopwatch stopwatch,
            TimeSpan deadline, CancellationToken token)
        {
            TimeSpan? firstByteAt = null;
            var readBuffer = new byte[1];

            while (true)
            {
                token.ThrowIfCancellationRequested();

                var remaining = deadline - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero) return null;

                int bytesRead = await ReadWithTimeout(readBuffer, remaining, token).ConfigureAwait(false);
                if (bytesRead == 0) return null;

                // Time the first byte to reach us, whatever it is. The input buffer was cleared
                // before the write, so anything arriving now belongs to this reply.
                firstByteAt = firstByteAt ?? stopwatch.Elapsed;

                if (readBuffer[0] != Message.StartOfMessage) continue;

                buffer.Add(readBuffer[0]);
                return firstByteAt;
            }
        }

        private async Task<int> ReadWithTimeout(byte[] buffer, TimeSpan timeout, CancellationToken token)
        {
            using var timeoutSource = new CancellationTokenSource(timeout);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutSource.Token);

            try
            {
                return await _connection.ReadAsync(buffer, linkedSource.Token).ConfigureAwait(false);
            }
            catch
            {
                token.ThrowIfCancellationRequested();
                return 0;
            }
        }

        private ExchangeOutcome Classify(ExchangeRecord exchange, EchoRequest request, int expectedLength)
        {
            if (exchange.Outcome != ExchangeOutcome.Received) return exchange.Outcome;

            var reply = exchange.Reply;
            if (reply == null || !reply.IsDataCorrect) return ExchangeOutcome.IntegrityError;
            if (reply.Address != _options.Address) return ExchangeOutcome.PatternMismatch;
            if (reply.Type == (byte)ReplyType.Nak) return ExchangeOutcome.Nak;
            if (reply.Type != (byte)ReplyType.ManufactureSpecific) return ExchangeOutcome.PatternMismatch;

            if (!EchoResponse.TryParse(reply.Payload.AsSpan(), out var response))
            {
                return ExchangeOutcome.PatternMismatch;
            }

            if (response.SequenceNumber != request.SequenceNumber) return ExchangeOutcome.PatternMismatch;
            if (response.Status != EchoStatus.Success) return ExchangeOutcome.PatternMismatch;
            if (response.Data.Length != expectedLength) return ExchangeOutcome.PatternMismatch;

            return LineQualityProtocol.ValidatePattern(request.Pattern, response.Data)
                ? ExchangeOutcome.Received
                : ExchangeOutcome.PatternMismatch;
        }

        private void AdvanceControlSequence()
        {
            // OSDP section 5.9: sequence numbers cycle 1, 2, 3 and skip zero, which is reserved
            // for signalling that communications have been reset. The sequence advances even after
            // a timeout: holding it would ask the responder to replay its cached reply, and that
            // replay would be counted as a successful delivery.
            _controlSequence = (byte)(_controlSequence >= 3 ? 1 : _controlSequence + 1);
        }

        private byte NextTestSequence()
        {
            byte current = _testSequence;
            _testSequence = (byte)((_testSequence + 1) & 0xFF);
            return current;
        }

        private void Trace(TraceDirection direction, byte[] data)
        {
            var tracer = _options?.Tracer;
            if (tracer == null) return;

            // The outgoing buffer carries a leading driver byte that is not part of the packet.
            var packet = direction == TraceDirection.Output ? data.Skip(1).ToArray() : data;
            tracer(new TraceEntry(direction, _traceId, packet));
        }

        private void Report(string message, int baudRate, int sent, int total, int completedRates,
            int totalRates)
        {
            _options?.Progress?.Report(new LineQualityProgress(message, baudRate, sent, total,
                completedRates, totalRates));
        }

        private readonly struct MatrixEntry
        {
            public MatrixEntry(TestPattern pattern, int payloadLength)
            {
                Pattern = pattern;
                PayloadLength = payloadLength;
            }

            public TestPattern Pattern { get; }

            public int PayloadLength { get; }
        }

        private enum ExchangeOutcome
        {
            Received,
            Timeout,
            IntegrityError,
            Nak,
            PatternMismatch
        }

        private readonly struct ExchangeRecord
        {
            private ExchangeRecord(ExchangeOutcome outcome, int commandLength, double responseTimeMs,
                IncomingMessage reply)
            {
                Outcome = outcome;
                CommandLength = commandLength;
                ResponseTimeMs = responseTimeMs;
                Reply = reply;
            }

            public ExchangeOutcome Outcome { get; }

            public int CommandLength { get; }

            public double ResponseTimeMs { get; }

            public IncomingMessage Reply { get; }

            public static ExchangeRecord TimedOut(int commandLength) =>
                new ExchangeRecord(ExchangeOutcome.Timeout, commandLength, 0.0, null);

            public static ExchangeRecord Truncated(int commandLength, double responseTimeMs) =>
                new ExchangeRecord(ExchangeOutcome.IntegrityError, commandLength, responseTimeMs, null);

            public static ExchangeRecord Replied(int commandLength, double responseTimeMs,
                IncomingMessage reply) =>
                new ExchangeRecord(ExchangeOutcome.Received, commandLength, responseTimeMs, reply);
        }

    }

    /// <summary>
    /// Raised when a line quality test cannot be run.
    /// </summary>
    public class LineQualityException : OSDPNetException
    {
        /// <summary>
        /// Initializes the exception.
        /// </summary>
        /// <param name="message">A description of what went wrong.</param>
        public LineQualityException(string message) : base(message)
        {
        }
    }
}
