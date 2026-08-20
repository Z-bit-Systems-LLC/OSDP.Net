using System;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OSDP.Net.Connections;
using OSDP.Net.Tracing;

namespace OSDP.Net.LineQuality.Cli
{
    /// <summary>
    /// Implementations of the three verbs.
    /// </summary>
    internal static class Verbs
    {
        private static readonly int BaselineBaudRate =
            LineQualityProtocol.ToBaudRate(LineQualityBaudRate.Baud9600);

        /// <summary>Lists the serial ports this machine can see.</summary>
        public static int Ports()
        {
            var portNames = SerialPort.GetPortNames().OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (portNames.Length == 0)
            {
                Console.Out.WriteLine("No serial ports found.");
                return ExitCodes.Error;
            }

            Console.Out.WriteLine("Available serial ports:");
            foreach (string name in portNames)
            {
                Console.Out.WriteLine($"  {name}");
            }

            return ExitCodes.Pass;
        }

        /// <summary>Runs the test as the controller.</summary>
        public static async Task<int> Run(CommandLineArguments arguments, CancellationToken token)
        {
            string portName = arguments.GetRequired("port");
            bool quiet = arguments.HasFlag("quiet");

            var options = new LineQualityOptions
            {
                Profile = arguments.GetProfile("profile", TestProfile.Screening),
                BaudRates = arguments.GetBaudRates("rates"),
                Address = arguments.GetByte("address", LineQualityProtocol.TestAddress),
                ResponseTimeout = TimeSpan.FromMilliseconds(arguments.GetInt32("timeout-ms", 200)),
                ReturnToBaselineWhenDone = !arguments.HasFlag("no-return")
            };

            if (!quiet)
            {
                options.Progress = new Progress<LineQualityProgress>(WriteProgress);
            }

            // A responder powers up at 9600, so that is where contact is made regardless of which
            // rates are being swept.
            using var connection = new SerialPortOsdpConnection(portName, BaselineBaudRate);
            using var capture = CreateCapture(arguments.GetOptional("osdpcap"), portName);

            if (capture != null)
            {
                options.Tracer = capture.WriteTrace;
            }

            var test = new LineQualityTest(connection, CreateLoggerFactory(quiet));

            Console.Out.WriteLine($"Testing {portName} at address {options.Address} " +
                                  $"using the {options.Profile} profile. Press Ctrl+C to stop.");

            var report = await test.RunAsync(options, token).ConfigureAwait(false);

            ClearProgressLine(quiet);
            ReportWriter.WriteSummary(report);

            string jsonPath = arguments.GetOptional("json");
            if (jsonPath != null)
            {
                ReportWriter.WriteJson(report, jsonPath);
            }

            string markdownPath = arguments.GetOptional("markdown");
            if (markdownPath != null)
            {
                ReportWriter.WriteMarkdown(report, markdownPath, BuildMetadata(arguments, portName));
            }

            return ExitCodes.FromVerdict(report.OverallVerdict);
        }

        /// <summary>Runs as the responder until interrupted.</summary>
        public static async Task<int> Respond(CommandLineArguments arguments, CancellationToken token)
        {
            string portName = arguments.GetRequired("port");
            bool quiet = arguments.HasFlag("quiet");
            int baudRate = arguments.GetInt32("baud", BaselineBaudRate);

            using var connection = new SerialPortOsdpConnection(portName, baudRate);

            var responder = new LineQualityResponder(connection,
                arguments.GetByte("address", LineQualityProtocol.TestAddress),
                CreateLoggerFactory(quiet))
            {
                AutoRevertTimeout = arguments.HasFlag("no-auto-revert")
                    ? Timeout.InfiniteTimeSpan
                    : TimeSpan.FromSeconds(arguments.GetInt32("auto-revert-seconds", 30))
            };

            long exchanges = 0;
            if (!quiet)
            {
                responder.ExchangeCompleted += (_, args) =>
                {
                    exchanges++;
                    Console.Error.Write($"\r{exchanges} exchanges at {args.BaudRate} baud   ");
                };

                responder.BaudRateChanged += (_, args) =>
                {
                    // On its own line, because the rate the responder is left on determines
                    // whether the next controller can find it at all.
                    Console.Error.WriteLine();
                    Console.Error.WriteLine(args.WasAutoRevert
                        ? $"Idle timeout: reverted to {args.BaudRate} baud"
                        : $"Switched to {args.BaudRate} baud");
                };
            }

            Console.Out.WriteLine($"Responding on {portName} at address " +
                                  $"{arguments.GetByte("address", LineQualityProtocol.TestAddress)}, " +
                                  $"{baudRate} baud. Press Ctrl+C to stop.");

            try
            {
                await responder.RunAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C is how this verb is meant to end.
            }

            ClearProgressLine(quiet);
            Console.Out.WriteLine($"Handled {exchanges} exchanges.");
            return ExitCodes.Pass;
        }

        /// <summary>
        /// Collects the installation details a report needs but the test cannot discover.
        /// </summary>
        private static LineQualityReportMetadata BuildMetadata(CommandLineArguments arguments,
            string portName)
        {
            return new LineQualityReportMetadata
            {
                TesterName = arguments.GetOptional("tester"),
                InstallationLocation = arguments.GetOptional("location"),
                CableDescription = arguments.GetOptional("cable"),
                PdDescription = arguments.GetOptional("responder"),
                Notes = arguments.GetOptional("notes"),
                AcuDescription = $"OSDP.Net osdp-linequality {ToolVersion()}",
                AdapterDescription = $"{Environment.OSVersion.VersionString}, {portName}",

                // Left unset unless the operator says so: the tool cannot read an adapter's
                // latency timer, and recording a guess would undermine the very timing figures
                // this field exists to qualify.
                AdapterLatencyTimerAdjusted = arguments.HasFlag("latency-timer-adjusted") ? true : null
            };
        }

        private static string ToolVersion() =>
            typeof(Verbs).Assembly.GetName().Version?.ToString(3) ?? "unknown";

        private static OSDPCaptureFileWriter CreateCapture(string path, string source) =>
            path == null ? null : new OSDPCaptureFileWriter(path, source, append: false);

        private static ILoggerFactory CreateLoggerFactory(bool quiet) =>
            LoggerFactory.Create(builder => builder
                .AddConsole()
                .SetMinimumLevel(quiet ? LogLevel.Error : LogLevel.Warning));

        private static void WriteProgress(LineQualityProgress progress)
        {
            // Progress goes to stderr so stdout stays clean enough to pipe.
            string detail = progress.TotalPacketsAtRate > 0
                ? $"{progress.PacketsSentAtRate}/{progress.TotalPacketsAtRate} packets"
                : "…";

            Console.Error.Write($"\r[{progress.CompletedBaudRates + 1}/{progress.TotalBaudRates}] " +
                                $"{progress.Message}: {detail}          ");
        }

        private static void ClearProgressLine(bool quiet)
        {
            if (quiet) return;

            Console.Error.Write("\r" + new string(' ', 78) + "\r");
        }
    }

    internal static class ExitCodes
    {
        public const int Pass = 0;
        public const int Marginal = 1;
        public const int Fail = 2;
        public const int Error = 3;

        public static int FromVerdict(LineQualityVerdict verdict)
        {
            switch (verdict)
            {
                case LineQualityVerdict.Pass: return Pass;
                case LineQualityVerdict.Marginal: return Marginal;
                case LineQualityVerdict.Fail: return Fail;
                default: return Error;
            }
        }
    }
}
