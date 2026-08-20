using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OSDP.Net.LineQuality.Cli
{
    /// <summary>
    /// Entry point for the osdp-linequality tool, which drives the OSDP Line Quality Test
    /// Procedure from a terminal.
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length == 0)
            {
                WriteUsage();
                return ExitCodes.Error;
            }

            string verb = args[0];

            if (IsHelp(verb))
            {
                WriteUsage();
                return ExitCodes.Pass;
            }

            string command = verb.ToLowerInvariant();
            if (command != "run" && command != "respond" && command != "ports")
            {
                // Check the verb before parsing options, otherwise a mistyped command surfaces as
                // a confusing complaint about its arguments.
                WriteError($"Unknown command '{verb}'.");
                WriteUsage();
                return ExitCodes.Error;
            }

            var cancellation = new CancellationTokenSource();

            // Cancel the run rather than killing the process, so the controller gets the chance to
            // put the responder back on 9600 before exiting.
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;

                // ReSharper disable once AccessToDisposedClosure
                // The finally block unsubscribes this handler before disposing the source, so it
                // cannot run against a disposed instance.
                cancellation.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;

            try
            {
                var arguments = CommandLineArguments.Parse(args.Skip(1));

                switch (command)
                {
                    case "run":
                        return await Verbs.Run(arguments, cancellation.Token).ConfigureAwait(false);
                    case "respond":
                        return await Verbs.Respond(arguments, cancellation.Token).ConfigureAwait(false);
                    default:
                        return Verbs.Ports();
                }
            }
            catch (ArgumentException exception)
            {
                WriteError(exception.Message);
                return ExitCodes.Error;
            }
            catch (LineQualityException exception)
            {
                WriteError(exception.Message);
                return ExitCodes.Error;
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Cancelled.");
                return ExitCodes.Error;
            }
            catch (Exception exception)
            {
                WriteError(exception.Message);
                return ExitCodes.Error;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
                cancellation.Dispose();
            }
        }

        private static bool IsHelp(string verb) =>
            verb.Equals("help", StringComparison.OrdinalIgnoreCase) ||
            verb.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
            verb.Equals("-h", StringComparison.OrdinalIgnoreCase);

        private static void WriteError(string message)
        {
            Console.Error.WriteLine($"error: {message}");
            Console.Error.WriteLine("Run 'osdp-linequality help' for usage.");
        }

        private static void WriteUsage()
        {
            Console.Out.WriteLine(@"osdp-linequality - OSDP RS-485 line quality tester

Measures packet loss, corruption and response timing across baud rates by
bouncing known bit patterns off a responder at the dedicated test address.

USAGE
  osdp-linequality <command> [options]

COMMANDS
  run        Act as the controller and produce a report
  respond    Act as the test responder
  ports      List available serial ports
  help       Show this message

RUN OPTIONS
  --port <name>            Serial port, e.g. COM3            (required)
  --profile <name>         screening | qualification | extended
                           Sets iterations per combination and therefore the
                           smallest loss rate the run can detect.
                           screening      160 packets/rate, detects >1.9%
                           qualification  960 packets/rate, detects >0.31%
                           extended      3200 packets/rate, detects >0.094%
                           (default: screening)
  --rates <list>           Comma-separated baud rates to sweep
                           (default: 9600,19200,38400,57600,115200,230400)
  --address <n>            Responder address (default: 125)
  --timeout-ms <n>         Reply window in milliseconds (default: 200)
  --json <path>            Write the full report as JSON
  --markdown <path>        Write a commissioning report as Markdown
  --osdpcap <path>         Capture packets to a Wireshark-compatible file
  --no-return              Leave the responder at the last rate tested
  --quiet                  Suppress progress output

REPORT DETAILS (optional, used by --markdown)
  --tester <name>          Who ran the test
  --location <text>        Installation location
  --cable <text>           Cable type and length
  --responder <text>       Responder model and firmware
  --notes <text>           Free-form notes to include
  --latency-timer-adjusted
                           Record that the adapter's latency timer was lowered
                           before the run, which is what makes the reported
                           response times meaningful

RESPOND OPTIONS
  --port <name>            Serial port, e.g. COM7            (required)
  --address <n>            Address to answer at (default: 125)
  --baud <n>               Starting baud rate (default: 9600)
  --auto-revert-seconds <n>
                           Idle time before falling back to 9600 (default: 30)
  --no-auto-revert         Never fall back to 9600
  --quiet                  Suppress progress output

EXIT CODES
  0  every tested rate passed
  1  best result was marginal
  2  one or more rates failed
  3  the test could not run

EXAMPLES
  osdp-linequality ports
  osdp-linequality respond --port COM7
  osdp-linequality run --port COM3 --profile qualification --json report.json
  osdp-linequality run --port COM3 --profile qualification \
      --markdown report.md --tester ""A Tech"" --cable ""Belden 9841, 150m""

NOTE
  Response times on a PC are limited by the serial adapter's latency timer,
  commonly 16 ms on FTDI parts. Treat timing as a pass/fail gate against the
  200 ms reply window rather than a precise measurement.");
        }
    }
}
