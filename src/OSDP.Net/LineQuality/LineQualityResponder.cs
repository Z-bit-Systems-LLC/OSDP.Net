using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OSDP.Net.Connections;
using OSDP.Net.Messages;
using OSDP.Net.Model;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.LineQuality
{
    /// <summary>
    /// The responder side of the OSDP Line Quality Test Procedure: answers echo requests at the
    /// dedicated test address and follows the controller through baud rate changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This deliberately does not build on <see cref="Device"/>. A line quality responder does not
    /// implement osdp_POLL, osdp_ID, osdp_CAP, osdp_COMSET or secure channel, and it has to retune
    /// its own port mid-session, which the connection-listener architecture is not shaped for.
    /// Owning the receive loop keeps the baud rate change under this class's control.
    /// </para>
    /// <para>
    /// Commands that fail their integrity check are met with silence rather than a NAK. The
    /// controller counts the resulting timeout, which is the honest outcome: a frame whose CRC
    /// failed cannot be trusted to have come from this address in the first place.
    /// </para>
    /// </remarks>
    public sealed class LineQualityResponder
    {
        private static readonly TimeSpan IdleReadWait = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan ReconnectDelay = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Consecutive read failures tolerated before the connection is reopened. More than one,
        /// because a single failure can be transient; small, because until the port is cleared the
        /// responder is not answering anyone.
        /// </summary>
        private const int ReadFaultsBeforeReconnect = 3;

        private readonly IRetunableOsdpConnection _connection;
        private readonly byte _address;
        private readonly ILogger _logger;

        private DateTime _lastValidCommand = DateTime.MinValue;
        private int _consecutiveReadFaults;

        /// <summary>
        /// Initializes a line quality responder.
        /// </summary>
        /// <param name="connection">The serial connection to answer on. The responder takes over
        /// the connection and retunes it during baud rate changes.</param>
        /// <param name="address">Address to answer at. Defaults to the dedicated test address 125.</param>
        /// <param name="loggerFactory">Optional logger factory.</param>
        /// <exception cref="ArgumentNullException">The connection is null.</exception>
        public LineQualityResponder(IRetunableOsdpConnection connection,
            byte address = LineQualityProtocol.TestAddress, ILoggerFactory loggerFactory = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _address = address;
            _logger = loggerFactory?.CreateLogger<LineQualityResponder>();
        }

        /// <summary>
        /// Gets or sets how long the responder waits without a valid command before falling back
        /// to 9600 baud. Defaults to 30 seconds; set to <see cref="Timeout.InfiniteTimeSpan"/> to
        /// disable.
        /// </summary>
        /// <remarks>
        /// The specification requires only that a responder power up at 9600. Reverting on idle as
        /// well means a baud rate change the line cannot sustain recovers by itself, instead of
        /// needing someone to walk over and power cycle the device.
        /// </remarks>
        public TimeSpan AutoRevertTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets how long to wait after a baud rate change acknowledgment has drained
        /// before retuning the port. Defaults to the 100 ms the specification requires.
        /// </summary>
        public TimeSpan BaudRateSettleDelay { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>Gets the baud rate the responder is currently answering at.</summary>
        public int CurrentBaudRate => _connection.BaudRate;

        /// <summary>
        /// Raised after each exchange, for progress display and diagnostics.
        /// </summary>
        public event EventHandler<LineQualityExchangeEventArgs> ExchangeCompleted;

        /// <summary>
        /// Raised whenever the responder retunes, either because the controller asked it to or
        /// because the idle timeout returned it to the baseline.
        /// </summary>
        /// <remarks>
        /// Section 8.3 of the test procedure asks a responder to indicate its current rate. Without
        /// this, a responder left on a high rate after an interrupted run is invisible, and the
        /// next controller that fails to find it has no way to tell why.
        /// </remarks>
        public event EventHandler<LineQualityBaudRateChangedEventArgs> BaudRateChanged;

        /// <summary>
        /// Runs the responder until the token is cancelled.
        /// </summary>
        /// <param name="token">Token used to stop the responder.</param>
        public async Task RunAsync(CancellationToken token)
        {
            if (!_connection.IsOpen)
            {
                await _connection.Open().ConfigureAwait(false);
            }

            // An ACU discards its buffers before each command because it drives a strict
            // command/reply cycle. A responder must not: the next command can already be arriving
            // while this reply is being written.
            _connection.DiscardBuffersBeforeWrite = false;
            _lastValidCommand = DateTime.UtcNow;

            _logger?.LogInformation(
                "Line quality responder listening at address {Address} on {Connection}, {BaudRate} baud",
                _address, _connection, _connection.BaudRate);

            while (!token.IsCancellationRequested)
            {
                var command = await ReadCommand(token).ConfigureAwait(false);
                if (command == null)
                {
                    RevertBaudRateIfIdle();
                    continue;
                }

                _lastValidCommand = DateTime.UtcNow;

                try
                {
                    await Respond(command, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger?.LogError(exception, "Failed to reply to a line quality command");
                }
            }
        }

        /// <summary>
        /// Reads the next well-formed command addressed to this responder, or null when the read
        /// timed out or the frame was not usable.
        /// </summary>
        private async Task<IncomingMessage> ReadCommand(CancellationToken token)
        {
            var buffer = new Collection<byte>();

            if (!await WaitForStartOfMessage(buffer, token).ConfigureAwait(false))
            {
                return null;
            }

            if (!await Bus.WaitForMessageLength(_connection, buffer, token).ConfigureAwait(false))
            {
                return null;
            }

            if (!await Bus.WaitForRestOfMessage(_connection, buffer, Bus.ExtractMessageLength(buffer), token)
                    .ConfigureAwait(false))
            {
                return null;
            }

            IncomingMessage command;
            try
            {
                command = new IncomingMessage(buffer.ToArray(), ClearTextChannel.Instance);
            }
            catch (Exception exception)
            {
                // A frame mangled badly enough to fail parsing is indistinguishable from line
                // noise. Stay silent and let the controller record a timeout.
                _logger?.LogDebug(exception, "Discarded an unparsable frame");
                return null;
            }

            if (!command.IsDataCorrect)
            {
                _logger?.LogDebug("Discarded a frame that failed its integrity check");
                return null;
            }

            return command.Address == _address ? command : null;
        }

        /// <summary>
        /// Waits for the start of a message, telling an idle line apart from a faulted port.
        /// </summary>
        /// <remarks>
        /// The shared <see cref="Bus"/> read helpers swallow every exception and report "no data",
        /// which is right for an ACU driving a command/reply cycle but wrong here. A serial port
        /// left in a fault state by line noise reports failures indefinitely, and a responder that
        /// cannot tell that apart from silence spins forever without answering and without saying
        /// why. Observed on the bench: after a badly degraded run the responder stopped answering
        /// permanently, while a freshly started one on the same wiring worked immediately.
        /// </remarks>
        private async Task<bool> WaitForStartOfMessage(Collection<byte> buffer, CancellationToken token)
        {
            var deadline = DateTime.UtcNow + IdleReadWait;
            var readBuffer = new byte[1];

            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();

                int bytesRead;
                try
                {
                    bytesRead = await ReadWithTimeout(readBuffer, IdleReadWait, token).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    await HandleReadFault(exception, token).ConfigureAwait(false);
                    return false;
                }

                _consecutiveReadFaults = 0;

                // A genuinely idle line, which is the normal case between commands.
                if (bytesRead == 0) return false;

                if (readBuffer[0] != Message.StartOfMessage) continue;

                buffer.Add(readBuffer[0]);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reads with a timeout, returning zero on an idle line and letting real faults through.
        /// </summary>
        /// <remarks>
        /// The distinction is made on <em>which token fired</em>, not on the exception type.
        /// Connection implementations disagree about how a read timeout surfaces:
        /// <see cref="SerialPortOsdpConnection"/> races the read against the token and throws
        /// <see cref="TimeoutException"/>, while a cancelled stream read normally produces
        /// <see cref="OperationCanceledException"/>. Keying off the type alone therefore classifies
        /// an ordinary idle line as a hardware fault on the very connection this responder runs on,
        /// which is the opposite of the intent and floods the log with warnings that mean nothing.
        /// </remarks>
        private async Task<int> ReadWithTimeout(byte[] buffer, TimeSpan timeout, CancellationToken token)
        {
            using var timeoutSource = new CancellationTokenSource(timeout);
            using var linkedSource =
                CancellationTokenSource.CreateLinkedTokenSource(token, timeoutSource.Token);

            try
            {
                return await _connection.ReadAsync(buffer, linkedSource.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (timeoutSource.IsCancellationRequested &&
                                              IsTimeoutSignal(exception))
            {
                // Our own read timeout expired: the line was simply quiet. Cancellation of the run
                // still propagates; anything else is a genuine fault and is left to the caller.
                token.ThrowIfCancellationRequested();
                return 0;
            }
        }

        /// <summary>
        /// Determines whether an exception is how a connection reports an expired read timeout.
        /// </summary>
        private static bool IsTimeoutSignal(Exception exception) =>
            exception is OperationCanceledException || exception is TimeoutException;

        private async Task HandleReadFault(Exception exception, CancellationToken token)
        {
            // A read that fails while the responder is being shut down is a consequence of the
            // shutdown, not a fault worth reopening the port over.
            if (token.IsCancellationRequested) return;

            _consecutiveReadFaults++;

            _logger?.LogWarning(exception, "Serial read failed ({Count} in a row)",
                _consecutiveReadFaults);

            if (_consecutiveReadFaults < ReadFaultsBeforeReconnect) return;

            _consecutiveReadFaults = 0;
            await ReopenConnection(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Closes and reopens the connection to clear a port stuck in a fault state.
        /// </summary>
        private async Task ReopenConnection(CancellationToken token)
        {
            int baudRate = _connection.BaudRate;

            try
            {
                await _connection.Close().ConfigureAwait(false);
                await Task.Delay(ReconnectDelay, token).ConfigureAwait(false);
                await _connection.Open().ConfigureAwait(false);

                _connection.SetBaudRate(baudRate);
                _connection.DiscardBuffersBeforeWrite = false;

                // Warning rather than Information: reopening a port is an abnormal event, and a
                // consumer that filters to Warning is exactly the one that needs to see it.
                _logger?.LogWarning(
                    "Reopened the connection at {BaudRate} baud to clear repeated read failures",
                    baudRate);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Could not reopen the connection after read failures");
            }
        }

        private async Task Respond(IncomingMessage command, CancellationToken token)
        {
            if (command.Type != (byte)CommandType.ManufacturerSpecific)
            {
                await SendReply(command, new Nak(ErrorCode.UnknownCommandCode), token).ConfigureAwait(false);
                return;
            }

            var payload = command.Payload.AsSpan();

            if (EchoRequest.TryParse(payload, out var echoRequest))
            {
                await HandleEcho(command, echoRequest, token).ConfigureAwait(false);
                return;
            }

            if (BaudRateChange.TryParse(payload, out var baudRateChange))
            {
                await HandleBaudRateChange(command, baudRateChange, token).ConfigureAwait(false);
                return;
            }

            await SendReply(command, new Nak(ErrorCode.UnknownCommandCode), token).ConfigureAwait(false);
        }

        private async Task HandleEcho(IncomingMessage command, EchoRequest request, CancellationToken token)
        {
            PayloadData reply;

            if (!LineQualityProtocol.IsSupportedPattern(request.Pattern))
            {
                reply = new EchoResponse(request.SequenceNumber, EchoStatus.UnsupportedPattern);
            }
            else if (request.DeclaredPayloadLength > LineQualityProtocol.MaxPayloadLength)
            {
                reply = new EchoResponse(request.SequenceNumber, EchoStatus.LengthError);
            }
            else if (!request.IsLengthConsistent)
            {
                // The header promised more data than the frame carried, yet the CRC passed. The
                // frame is internally inconsistent rather than corrupted in transit.
                reply = new Nak(ErrorCode.InvalidCommandLength);
            }
            else
            {
                // Echo the bytes as received. Regenerating them from the pattern would repair a
                // corrupted payload on the way back and hide the very errors being measured.
                reply = new EchoResponse(request.SequenceNumber, request.Data);
            }

            await SendReply(command, reply, token).ConfigureAwait(false);

            OnExchangeCompleted(new LineQualityExchangeEventArgs(request.SequenceNumber, request.Pattern,
                request.Data.Length, (reply as EchoResponse)?.Status, _connection.BaudRate));
        }

        private async Task HandleBaudRateChange(IncomingMessage command, BaudRateChange request,
            CancellationToken token)
        {
            int newBaudRate = 0;
            var status = BaudRateChangeStatus.UnsupportedRate;

            try
            {
                newBaudRate = LineQualityProtocol.ToBaudRate(request.BaudRateId);
                status = BaudRateChangeStatus.Success;
            }
            catch (ArgumentOutOfRangeException)
            {
                _logger?.LogWarning("Rejected unsupported baud rate ID {BaudRateId}", request.BaudRateId);
            }

            int writtenBytes = await SendReply(command,
                new BaudRateChangeAck(request.SequenceNumber, status), token).ConfigureAwait(false);

            if (status != BaudRateChangeStatus.Success) return;

            // The acknowledgment goes out at the old rate. Retuning before it has physically left
            // the line would corrupt its tail, so drain first, then observe the settle delay.
            await _connection.WaitForTransmitCompleteAsync(writtenBytes, token).ConfigureAwait(false);
            await Task.Delay(BaudRateSettleDelay, token).ConfigureAwait(false);

            _connection.SetBaudRate(newBaudRate);
            _lastValidCommand = DateTime.UtcNow;

            _logger?.LogInformation("Switched to {BaudRate} baud", newBaudRate);
            BaudRateChanged?.Invoke(this, new LineQualityBaudRateChangedEventArgs(newBaudRate, false));
        }

        private async Task<int> SendReply(IncomingMessage command, PayloadData reply, CancellationToken token)
        {
            var message = new OutgoingReply(command, reply).BuildMessage(ClearTextChannel.Instance);

            token.ThrowIfCancellationRequested();
            await _connection.WriteAsync(message).ConfigureAwait(false);

            return message.Length;
        }

        private void RevertBaudRateIfIdle()
        {
            if (AutoRevertTimeout == Timeout.InfiniteTimeSpan) return;

            int fallbackRate = LineQualityProtocol.ToBaudRate(LineQualityBaudRate.Baud9600);
            if (_connection.BaudRate == fallbackRate) return;
            if (DateTime.UtcNow - _lastValidCommand < AutoRevertTimeout) return;

            _logger?.LogWarning(
                "No valid command for {Timeout}; reverting from {Current} to {Fallback} baud",
                AutoRevertTimeout, _connection.BaudRate, fallbackRate);

            _connection.SetBaudRate(fallbackRate);
            _lastValidCommand = DateTime.UtcNow;

            BaudRateChanged?.Invoke(this, new LineQualityBaudRateChangedEventArgs(fallbackRate, true));
        }

        private void OnExchangeCompleted(LineQualityExchangeEventArgs args) =>
            ExchangeCompleted?.Invoke(this, args);
    }

    /// <summary>
    /// Describes a completed echo exchange handled by a <see cref="LineQualityResponder"/>.
    /// </summary>
    public class LineQualityExchangeEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes the event data.
        /// </summary>
        /// <param name="sequenceNumber">The test sequence number of the request.</param>
        /// <param name="pattern">The requested pattern.</param>
        /// <param name="payloadLength">Number of test data bytes echoed.</param>
        /// <param name="status">The status replied with, or null when the reply was a NAK.</param>
        /// <param name="baudRate">The rate the exchange took place at.</param>
        public LineQualityExchangeEventArgs(byte sequenceNumber, TestPattern pattern, int payloadLength,
            EchoStatus? status, int baudRate)
        {
            SequenceNumber = sequenceNumber;
            Pattern = pattern;
            PayloadLength = payloadLength;
            Status = status;
            BaudRate = baudRate;
        }

        /// <summary>Gets the test sequence number of the request.</summary>
        public byte SequenceNumber { get; }

        /// <summary>Gets the requested pattern.</summary>
        public TestPattern Pattern { get; }

        /// <summary>Gets the number of test data bytes echoed.</summary>
        public int PayloadLength { get; }

        /// <summary>Gets the status replied with, or null when the reply was a NAK.</summary>
        public EchoStatus? Status { get; }

        /// <summary>Gets the baud rate the exchange took place at.</summary>
        public int BaudRate { get; }
    }

    /// <summary>
    /// Describes a change in the rate a <see cref="LineQualityResponder"/> is answering at.
    /// </summary>
    public class LineQualityBaudRateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes the event data.
        /// </summary>
        /// <param name="baudRate">The rate the responder is now using.</param>
        /// <param name="wasAutoRevert">Whether the change was the idle timeout returning the
        /// responder to the baseline rather than a request from the controller.</param>
        public LineQualityBaudRateChangedEventArgs(int baudRate, bool wasAutoRevert)
        {
            BaudRate = baudRate;
            WasAutoRevert = wasAutoRevert;
        }

        /// <summary>Gets the rate the responder is now using.</summary>
        public int BaudRate { get; }

        /// <summary>
        /// Gets a value indicating whether the idle timeout caused the change, rather than the
        /// controller requesting it.
        /// </summary>
        public bool WasAutoRevert { get; }
    }
}
