using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OSDP.Net.Utilities
{
    /// <summary>
    /// Converts byte counts into the time they occupy on the communication channel, and enforces the
    /// idle line time OSDP v2.2.2 subclause 5.7 requires before a device may drive that channel.
    /// </summary>
    /// <remarks>
    /// The spec requires "an equivalent of two UTF-8 characters idle time before it may access the
    /// communication channel… to allow for signal converters and/or multiplexers to sense that the
    /// line has become idle." On an RS-485 bus a reply that starts sooner can arrive while the other
    /// device still has its transceiver driving the line, so the reply's leading bytes are lost and
    /// the sender retries.
    /// </remarks>
    internal static class LineTiming
    {
        /// <summary>
        /// Bits on the wire per character for the 8-N-1 framing OSDP uses: one start bit, eight data
        /// bits and one stop bit.
        /// </summary>
        private const int BitsPerCharacter = 10;

        /// <summary>
        /// Characters of idle time required before accessing the channel (subclause 5.7).
        /// </summary>
        private const int IdleCharacters = 2;

        /// <summary>
        /// Milliseconds below which <see cref="WaitAsync"/> spins rather than sleeps. The runtime's
        /// timer granularity is far coarser than the sub-millisecond precision these delays need: a
        /// two millisecond sleep routinely overshoots to roughly sixteen, which would throttle the
        /// whole polling loop.
        /// </summary>
        private const double SpinThresholdMilliseconds = 16;

        /// <summary>
        /// Calculates the time the given number of characters occupies on the wire.
        /// </summary>
        /// <param name="baudRate">Line rate in bits per second.</param>
        /// <param name="characterCount">Number of characters (bytes) transmitted.</param>
        /// <returns>
        /// The transmission time, or <see cref="TimeSpan.Zero"/> when the baud rate is unknown.
        /// </returns>
        public static TimeSpan ForCharacters(int baudRate, int characterCount) =>
            baudRate <= 0 || characterCount <= 0
                ? TimeSpan.Zero
                : TimeSpan.FromSeconds((double)(characterCount * BitsPerCharacter) / baudRate);

        /// <summary>
        /// Calculates the idle line time required before a device may drive the channel at the given
        /// line rate, per OSDP v2.2.2 subclause 5.7.
        /// </summary>
        /// <param name="baudRate">Line rate in bits per second.</param>
        /// <returns>
        /// The time two characters occupy on the wire, or <see cref="TimeSpan.Zero"/> when the baud
        /// rate is unknown.
        /// </returns>
        public static TimeSpan IdleLine(int baudRate) => ForCharacters(baudRate, IdleCharacters);

        /// <summary>
        /// Waits until <paramref name="delay"/> has elapsed since <paramref name="sinceTimestamp"/>,
        /// returning immediately if it already has.
        /// </summary>
        /// <param name="delay">The time to enforce.</param>
        /// <param name="sinceTimestamp">
        /// A <see cref="Stopwatch.GetTimestamp"/> value taken when the line last went idle.
        /// </param>
        public static async Task WaitAsync(TimeSpan delay, long sinceTimestamp)
        {
            if (delay <= TimeSpan.Zero) return;

            long target = sinceTimestamp + (long)(delay.TotalSeconds * Stopwatch.Frequency);

            // A delay long enough to sleep through is slept off first, leaving only the imprecise
            // tail to spin. In practice the whole wait is spun: two characters is 2.08 ms at 9600
            // baud and less at every higher rate.
            double remaining = RemainingMilliseconds(target);
            if (remaining > SpinThresholdMilliseconds)
            {
                await Task.Delay((int)(remaining - SpinThresholdMilliseconds)).ConfigureAwait(false);
            }

            while (Stopwatch.GetTimestamp() < target)
            {
                Thread.SpinWait(50);
            }
        }

        private static double RemainingMilliseconds(long target) =>
            (target - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
    }
}
