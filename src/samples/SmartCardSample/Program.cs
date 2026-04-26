using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OSDP.Net;
using OSDP.Net.Connections;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using OSDP.Net.Tracing;

namespace SmartCardSample;

internal class Program
{
    private static Guid _connectionId;
    private static ControlPanel _panel = null!;
    private static byte _deviceAddress;
    private static byte _readerNumber;
    private static bool _deviceOnline;
    private static volatile bool _replActive;
    private static TaskCompletionSource _cardPresentSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static System.Timers.Timer? _pollTimer;

    private static async Task Main()
    {
        var builder = new ConfigurationBuilder().AddJsonFile("appsettings.json", true, true);
        var config = builder.Build();

        var osdpSection = config.GetSection("OSDP");
        string portName = osdpSection["PortName"]!;
        int baudRate = int.Parse(osdpSection["BaudRate"]!);
        _deviceAddress = byte.Parse(osdpSection["DeviceAddress"]!);
        _readerNumber = byte.Parse(osdpSection["ReaderNumber"]!);

        _panel = new ControlPanel(new NullLoggerFactory());

        _panel.ConnectionStatusChanged += (_, eventArgs) =>
        {
            _deviceOnline = eventArgs.IsConnected;
            Console.WriteLine();
            Console.WriteLine(
                $"Device is {(eventArgs.IsConnected ? "Online" : "Offline")} in {(eventArgs.IsSecureChannelEstablished ? "Secure" : "Clear Text")} mode");
        };

        _panel.NakReplyReceived += (_, args) =>
        {
            Console.WriteLine();
            Console.WriteLine($"Received NAK {args.Nak}");
        };

        _panel.RawCardDataReplyReceived += (_, eventArgs) =>
        {
            Console.WriteLine();
            Console.WriteLine("Received raw card data");
            Console.Write(eventArgs.RawCardData);
        };

        _panel.ExtendedReadReplyReceived += (_, eventArgs) => OnExtendedReadReply(eventArgs.ExtendedRead);

        _connectionId = _panel.StartConnection(
            new SerialPortOsdpConnection(portName, baudRate) { ReplyTimeout = TimeSpan.FromSeconds(2) },
            TimeSpan.FromMilliseconds(100),
            true);
        _panel.AddDevice(_connectionId, _deviceAddress, true, true);

        _pollTimer = new System.Timers.Timer(TimeSpan.FromSeconds(5).TotalMilliseconds) { AutoReset = false };
        _pollTimer.Elapsed += async (_, _) => await PollMode();
        _pollTimer.Start();

        using var exitCts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            // ReSharper disable once AccessToDisposedClosure
            exitCts.Cancel();
        };

        Console.WriteLine("SmartCardSample running. Press Ctrl+C to exit.");

        try
        {
            while (!exitCts.Token.IsCancellationRequested)
            {
                await _cardPresentSignal.Task.WaitAsync(exitCts.Token);
                _cardPresentSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                await RunApduRepl(exitCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful Ctrl+C exit
        }

        _pollTimer.Stop();
        _pollTimer.Dispose();
        await _panel.Shutdown();

        WriteParsedCapture(_connectionId);
    }

    private static void WriteParsedCapture(Guid connectionId)
    {
        var capturePath = $"{connectionId:D}.osdpcap";
        if (!File.Exists(capturePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(capturePath);
            var spy = new MessageSpy("0123456789:;<=>?"u8.ToArray());
            var formatter = new OSDPPacketTextFormatter();
            var sb = new StringBuilder();
            DateTime previous = DateTime.MinValue;

            foreach (var entry in spy.ParseCaptureFile(json))
            {
                TimeSpan? delta = previous > DateTime.MinValue ? entry.TimeStamp - previous : null;
                previous = entry.TimeStamp;
                sb.Append(formatter.FormatPacket(entry.Packet, entry.TimeStamp, delta));
            }

            var outputPath = Path.ChangeExtension(capturePath, ".txt");
            File.WriteAllText(outputPath, sb.ToString());
            Console.WriteLine();
            Console.WriteLine($"Parsed capture written to {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"Failed to parse capture file: {ex.Message}");
        }
    }

    private static void OnExtendedReadReply(ExtendedRead reply)
    {
        // Ignore replies that are responses to commands sent from the REPL —
        // only unsolicited "card present" notifications should drive the signal.
        if (_replActive) return;
        if (reply is { Mode: 1, PReply: 1 })
        {
            _cardPresentSignal.TrySetResult();
        }
    }

    private static async Task PollMode()
    {
        try
        {
            if (!_deviceOnline || _replActive) return;

            var reply = await _panel.ExtendedWriteData(_connectionId, _deviceAddress,
                ExtendedWrite.ReadModeSetting());

            if (reply.ReplyData == null)
            {
                Console.WriteLine();
                Console.WriteLine("No reply to ReadModeSetting — resetting device.");
                _panel.ResetDevice(_connectionId, _deviceAddress);
                return;
            }

            var pData = reply.ReplyData.PData.ToArray();
            if (reply.ReplyData.Mode == 0 && reply.ReplyData.PReply == 1 &&
                pData.Length > 0 && pData[0] == 0)
            {
                Console.WriteLine();
                Console.WriteLine("PD is in Mode 0 — switching to Mode 1.");
                await _panel.ExtendedWriteData(_connectionId, _deviceAddress,
                    ExtendedWrite.ModeOneConfiguration());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"PollMode exception: {ex.Message}");
        }
        finally
        {
            _pollTimer?.Start();
        }
    }

    private static async Task RunApduRepl(CancellationToken cancellationToken)
    {
        _replActive = true;
        try
        {
            var scanReply = await _panel.ExtendedWriteData(_connectionId, _deviceAddress,
                ExtendedWrite.ModeOneSmartCardScan(_readerNumber));

            if (scanReply.ReplyData is not { Mode: 1, PReply: 1 })
            {
                Console.WriteLine();
                Console.WriteLine("Smart card scan did not confirm card presence — aborting REPL.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Card Present. Enter APDU as hex (blank line to exit this card):");

            while (!cancellationToken.IsCancellationRequested)
            {
                Console.Write("> ");

                // Read on a background thread so we don't block any sync context.
                string? input = await Task.Run(Console.ReadLine, cancellationToken);
                if (string.IsNullOrWhiteSpace(input)) break;

                byte[] apdu;
                try
                {
                    apdu = StringToByteArray(input);
                }
                catch (FormatException)
                {
                    Console.WriteLine("Invalid hex.");
                    continue;
                }

                var apduReply = await _panel.ExtendedWriteData(_connectionId, _deviceAddress,
                    ExtendedWrite.ModeOnePassAPDUCommand(_readerNumber, apdu));

                if (apduReply.ReplyData != null)
                {
                    var responseBytes = apduReply.ReplyData.PData.ToArray();
                    Console.WriteLine(
                        $"{apduReply.ReplyData.Mode}:{apduReply.ReplyData.PReply}:{BitConverter.ToString(responseBytes)}");
                }
                else
                {
                    Console.WriteLine("No reply data.");
                }
            }

            try
            {
                await _panel.ExtendedWriteData(_connectionId, _deviceAddress,
                    ExtendedWrite.ModeOneTerminateSmartCardConnection(_readerNumber));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Terminate session failed: {ex.Message}");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"RunApduRepl exception: {ex.Message}");
        }
        finally
        {
            _replActive = false;
        }
    }

    private static byte[] StringToByteArray(string hex)
    {
        hex = hex.Replace(" ", string.Empty);
        return Enumerable.Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray();
    }
}
