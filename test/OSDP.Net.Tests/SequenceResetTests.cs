using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OSDP.Net.Connections;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests;

/// <summary>
/// Tests for ACU recovery when a PD resets and sends NAK(UnexpectedSequenceNumber)
/// at sequence 0.
///
/// Reproduces a real-world issue observed with Elatec Secustos readers where the PD
/// intermittently resets: it accepts sequence 0, but NAKs at sequence 0 for any
/// command with sequence greater than 0. The ACU must detect this and keep resetting
/// until the PD stabilizes.
/// </summary>
[TestFixture]
[Category("Unit")]
public class SequenceResetTests
{
    [Test]
    [CancelAfter(15000)]
    public async Task AcuRecoversConnection_WhenPdResetsAndNaksAtSequenceZero()
    {
        var mock = new FlakyPdConnection();
        var panel = new ControlPanel(NullLoggerFactory.Instance);

        var deviceOnline = new TaskCompletionSource<bool>();
        var deviceRecovered = new TaskCompletionSource<bool>();
        bool wasOnline = false;
        bool wentOffline = false;

        panel.ConnectionStatusChanged += (_, e) =>
        {
            if (e.IsConnected && !wasOnline)
            {
                wasOnline = true;
                deviceOnline.TrySetResult(true);
            }
            else if (!e.IsConnected && wasOnline && !wentOffline)
            {
                wentOffline = true;
            }
            else if (e.IsConnected && wentOffline)
            {
                deviceRecovered.TrySetResult(true);
            }
        };

        var connectionId = panel.StartConnection(mock);
        panel.AddDevice(connectionId, 0, true, false);

        var onlineResult = await Task.WhenAny(deviceOnline.Task, Task.Delay(5000));
        Assert.That(onlineResult, Is.EqualTo(deviceOnline.Task),
            "Device should come online initially");

        // Simulate a PD that keeps resetting: ACKs sequence 0, NAKs anything else
        // at sequence 0. The PD stabilizes after receiving sequence 0 three times.
        mock.SimulateFlakyPd(sequenceZeroCountToStabilize: 3);

        // The ACU should recover. Without the fix, after the first reset (caught by
        // the IsConnected && Sequence==0 check), the new DeviceProxy has IsConnected=false
        // and the Sequence>0 guard on the UnexpectedSequenceNumber NAK handler prevents
        // further resets, wedging the ACU at sequence 1.
        var recoveryResult = await Task.WhenAny(deviceRecovered.Task, Task.Delay(10000));
        Assert.That(recoveryResult, Is.EqualTo(deviceRecovered.Task),
            "ACU should recover from flaky PD - if this times out, the ACU is wedged in a " +
            "NAK loop (the Sequence > 0 guard prevents reset when IsConnected is false)");

        await panel.Shutdown();
    }

    /// <summary>
    /// Simulates a PD that intermittently resets.
    ///
    /// Normal mode: replies ACK at the command's sequence number.
    ///
    /// After SimulateFlakyPd(): the PD enters a "flaky reset" state where it ACKs
    /// commands at sequence 0 but NAKs (UnexpectedSequenceNumber) at sequence 0 for
    /// any command with sequence greater than 0. After receiving sequence 0 a specified
    /// number of times, the PD stabilizes and returns to normal operation.
    /// </summary>
    private sealed class FlakyPdConnection : IOsdpConnection
    {
        private readonly Pipe _replyPipe = new();

        private volatile bool _flakyMode;
        private int _sequenceZeroAckCount;
        private int _sequenceZeroCountToStabilize;

        public bool IsOpen => true;
        public int BaudRate => 9600;
        public TimeSpan ReplyTimeout { get; set; } = TimeSpan.FromSeconds(2);

        public Task Open() => Task.CompletedTask;
        public Task Close() => Task.CompletedTask;

        public void SimulateFlakyPd(int sequenceZeroCountToStabilize)
        {
            _sequenceZeroAckCount = 0;
            _sequenceZeroCountToStabilize = sequenceZeroCountToStabilize;
            _flakyMode = true;
        }

        public async Task WriteAsync(byte[] buffer)
        {
            IncomingMessage command;
            try
            {
                command = new IncomingMessage(buffer.Skip(1).ToArray(), new ACUMessageSecureChannel());
            }
            catch
            {
                return;
            }

            byte replySequence;
            PayloadData replyData;

            if (_flakyMode)
            {
                if (command.ControlBlock.Sequence == 0)
                {
                    replyData = new Ack();
                    replySequence = 0;
                    _sequenceZeroAckCount++;
                    if (_sequenceZeroAckCount >= _sequenceZeroCountToStabilize)
                    {
                        _flakyMode = false;
                    }
                }
                else
                {
                    replyData = new Nak(ErrorCode.UnexpectedSequenceNumber);
                    replySequence = 0;
                }
            }
            else
            {
                replyData = new Ack();
                replySequence = command.ControlBlock.Sequence;
            }

            var reply = new OutgoingMessage(
                0x80, new Control(replySequence, true, false), replyData);
            var replyBytes = reply.BuildMessage(new PdMessageSecureChannelBase());

            await _replyPipe.Writer.WriteAsync(replyBytes);
            await _replyPipe.Writer.FlushAsync();
        }

        public async Task<int> ReadAsync(byte[] buffer, CancellationToken token)
        {
            try
            {
                var result = await _replyPipe.Reader.ReadAsync(token);
                var readBuffer = result.Buffer;

                if (result.IsCanceled || (result.IsCompleted && readBuffer.IsEmpty))
                    return 0;

                var bytesToCopy = (int)Math.Min(buffer.Length, readBuffer.Length);
                var sliced = readBuffer.Slice(0, bytesToCopy);
                sliced.CopyTo(buffer.AsSpan());
                _replyPipe.Reader.AdvanceTo(sliced.End);
                return bytesToCopy;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
        }

        public void Dispose()
        {
            _replyPipe.Writer.Complete();
            _replyPipe.Reader.Complete();
        }
    }
}
