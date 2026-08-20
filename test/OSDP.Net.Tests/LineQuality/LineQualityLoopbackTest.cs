using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.LineQuality;

namespace OSDP.Net.Tests.LineQuality
{
    /// <summary>
    /// Drives the controller and responder against each other over an in-memory loopback. This
    /// covers the interaction between the two halves, which is the part unit tests on either side
    /// alone cannot reach.
    /// </summary>
    /// <remarks>
    /// Timing and signal integrity are deliberately out of scope here: an in-memory pipe delivers
    /// instantly and cannot drop a bit on its own. Those need two real RS-485 adapters on a cable.
    /// What this does prove is that the state machines agree, that baud rate changes are followed
    /// by both ends, and that each failure category is counted in the right bucket.
    /// </remarks>
    [TestFixture]
    public class LineQualityLoopbackTest
    {
        private const int TestBaudRate = 230400;

        [Test]
        public async Task CleanLoopback_PassesWithEveryPacketAccountedFor()
        {
            var (report, _) = await RunScreening(responderFilter: null);

            var result = report.BaudRates.Single();
            Assert.Multiple(() =>
            {
                // 16 combinations from section 3.10, 10 iterations each under the screening profile.
                Assert.That(result.PacketsSent, Is.EqualTo(160));
                Assert.That(result.PacketsReceived, Is.EqualTo(160));
                Assert.That(result.Failures, Is.Zero);
                Assert.That(result.Verdict, Is.EqualTo(LineQualityVerdict.Pass));
                Assert.That(report.OverallVerdict, Is.EqualTo(LineQualityVerdict.Pass));
                Assert.That(report.RecommendedBaudRate, Is.EqualTo(TestBaudRate));
            });
        }

        [Test]
        public async Task EveryCombinationFromTheMatrixIsExercised()
        {
            var (report, _) = await RunScreening(responderFilter: null);

            var combinations = report.BaudRates.Single().Combinations;

            Assert.Multiple(() =>
            {
                Assert.That(combinations, Has.Count.EqualTo(16));

                // The four constant patterns run at every size; sequential and walking-one have no
                // meaningful zero-length case.
                Assert.That(combinations.Count(c => c.PayloadLength == 0), Is.EqualTo(4));
                Assert.That(combinations.Count(c => c.PayloadLength == 48), Is.EqualTo(6));
                Assert.That(combinations.Count(c => c.PayloadLength == 113), Is.EqualTo(6));
                Assert.That(combinations.Select(c => c.Pattern).Distinct().Count(), Is.EqualTo(6));
            });
        }

        [Test]
        public async Task BaudRateChange_MovesBothEnds()
        {
            var (report, connections) = await RunScreening(responderFilter: null);

            Assert.Multiple(() =>
            {
                Assert.That(connections.Controller.BaudRate, Is.EqualTo(TestBaudRate));
                Assert.That(connections.Responder.BaudRate, Is.EqualTo(TestBaudRate));
                Assert.That(connections.Responder.BaudRateHistory, Does.Contain(TestBaudRate));
                Assert.That(report.BaudRates.Single().WasTested, Is.True);
            });
        }

        [Test]
        public async Task DroppedReplies_AreCountedAsTimeoutsAndFailTheRate()
        {
            // Silence from the responder is the signature of an open or unpowered line.
            int seen = 0;
            var (report, _) = await RunScreening(frame =>
            {
                // Let the handshake through, then swallow every reply once measurement starts.
                seen++;
                return seen > 3 ? null : frame;
            });

            var result = report.BaudRates.Single();
            Assert.Multiple(() =>
            {
                Assert.That(result.Timeouts, Is.EqualTo(result.PacketsSent));
                Assert.That(result.IntegrityErrors, Is.Zero);
                Assert.That(result.Naks, Is.Zero);
                Assert.That(result.Verdict, Is.EqualTo(LineQualityVerdict.Fail));
            });
        }

        [Test]
        public async Task CorruptedReplies_AreCountedAsIntegrityErrorsNotTimeouts()
        {
            // A reply that arrives but fails its CRC points at signal integrity rather than a
            // broken connection, so it has to land in a different bucket than a timeout.
            int seen = 0;
            var (report, _) = await RunScreening(frame =>
            {
                seen++;
                if (seen <= 3) return frame;

                var corrupted = (byte[])frame.Clone();

                // Flip a bit in the payload, leaving the CRC describing the original bytes.
                if (corrupted.Length > 10) corrupted[9] ^= 0xFF;
                return corrupted;
            });

            var result = report.BaudRates.Single();
            Assert.Multiple(() =>
            {
                Assert.That(result.IntegrityErrors, Is.GreaterThan(0));
                Assert.That(result.Timeouts, Is.Zero);
                Assert.That(result.PacketsReceived, Is.Zero);
                Assert.That(result.Verdict, Is.EqualTo(LineQualityVerdict.Fail));
            });
        }

        [Test]
        public void NoResponder_ReportsAClearFailureRatherThanAnEmptyReport()
        {
            var (controller, _) = LoopbackConnection.CreatePair(9600);
            var test = new LineQualityTest(controller);

            var options = new LineQualityOptions
            {
                Profile = TestProfile.Screening,
                BaudRates = new[] { TestBaudRate },
                ResponseTimeout = TimeSpan.FromMilliseconds(50),
                ReturnToBaselineWhenDone = false
            };

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var exception = Assert.ThrowsAsync<LineQualityException>(
                async () => await test.RunAsync(options, cancellation.Token));

            Assert.That(exception?.Message, Does.Contain("No line quality responder"));
        }

        [Test]
        public async Task FailedRecovery_ReturnsTheControllerToTheBaselineRate()
        {
            // Observed on a capacitor-loaded line: after one rate failed, the controller was left
            // on the highest rate it had probed during recovery, so every later rate change went
            // out over a line that had just proven it could not carry that rate. One real failure
            // became a cascade of invented ones.
            var (controller, _) = LoopbackConnection.CreatePair(9600);
            var test = new LineQualityTest(controller);

            var options = new LineQualityOptions
            {
                Profile = TestProfile.Screening,
                BaudRates = new[] { 38400, 115200, 230400 },
                ResponseTimeout = TimeSpan.FromMilliseconds(20),
                ReturnToBaselineWhenDone = false
            };

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            // With no responder at all, contact is never established and every rate is searched.
            Assert.ThrowsAsync<LineQualityException>(
                async () => await test.RunAsync(options, cancellation.Token));

            Assert.That(controller.BaudRate, Is.EqualTo(9600),
                "a failed search must leave the controller on the baseline, not the last rate tried");

            await Task.CompletedTask;
        }

        [Test]
        public void OversizedDeclaredLength_IsDetectableByTheResponder()
        {
            // A header that promises more payload than the frame carries, and more than the
            // guaranteed buffer. Both conditions have to be visible to the responder so it can
            // choose between a length-error status and a NAK.
            var payload = new byte[LineQualityProtocol.EchoHeaderLength];
            LineQualityProtocol.VendorCode.ToArray().CopyTo(payload, 0);
            payload[3] = LineQualityProtocol.EchoCommandId;
            payload[4] = 0x01;
            payload[5] = (byte)TestPattern.AllZeros;
            payload[6] = 0xFF;

            Assert.That(EchoRequest.TryParse(payload, out var parsed), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(parsed.DeclaredPayloadLength,
                    Is.GreaterThan(LineQualityProtocol.MaxPayloadLength));
                Assert.That(parsed.IsLengthConsistent, Is.False);
            });
        }

        private static async Task<(LineQualityReport Report,
                (LoopbackConnection Controller, LoopbackConnection Responder) Connections)>
            RunScreening(Func<byte[], byte[]> responderFilter)
        {
            var pair = LoopbackConnection.CreatePair(9600);

            var responder = new LineQualityResponder(pair.Responder)
            {
                BaudRateSettleDelay = TimeSpan.Zero,
                AutoRevertTimeout = Timeout.InfiniteTimeSpan
            };

            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var responderTask = responder.RunAsync(cancellation.Token);

            // Applied after the responder starts so the initial contact and baud change survive.
            pair.Responder.WriteFilter = responderFilter;

            var test = new LineQualityTest(pair.Controller);
            var options = new LineQualityOptions
            {
                Profile = TestProfile.Screening,
                BaudRates = new[] { TestBaudRate },
                BaudRateSettleDelay = TimeSpan.Zero,
                ResponseTimeout = TimeSpan.FromMilliseconds(50),
                ReturnToBaselineWhenDone = false
            };

            try
            {
                var report = await test.RunAsync(options, cancellation.Token).ConfigureAwait(false);
                return (report, pair);
            }
            finally
            {
                cancellation.Cancel();
                await responderTask.ConfigureAwait(false);
            }
        }
    }
}
