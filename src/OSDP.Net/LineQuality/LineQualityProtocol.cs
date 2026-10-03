using System;
using System.Collections.Generic;

namespace OSDP.Net.LineQuality
{
    /// <summary>
    /// Constants and helpers for the OSDP Line Quality Test Procedure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The procedure measures RS-485 physical layer quality by bouncing known bit patterns off a
    /// responder at a dedicated address and counting what comes back. It rides on osdp_MFG (0x80)
    /// and osdp_MFGREP (0x90) with a reserved vendor code, so test traffic coexists with production
    /// devices on the same bus without either interfering with the other.
    /// </para>
    /// <para>
    /// Test traffic is always sent in clear text with CRC-16. A secure channel would encrypt the
    /// payload and turn every pattern into statistically identical pseudo-random data.
    /// </para>
    /// </remarks>
    public static class LineQualityProtocol
    {
        /// <summary>
        /// The reserved vendor code identifying line quality test traffic, 02-00-0A.
        /// </summary>
        /// <remarks>
        /// Bit 1 of the first octet is the IEEE locally-administered bit, so this value lies
        /// outside the space IEEE assigns and cannot collide with a registered manufacturer.
        /// </remarks>
        internal static readonly byte[] VendorCodeBytes = { 0x02, 0x00, 0x0A };

        /// <summary>The dedicated line quality test address, 125 (0x7D).</summary>
        /// <remarks>
        /// Chosen to sit above the usual production range and below the 0x7F configuration
        /// address, while remaining a legal osdp_COMSET assignment target.
        /// </remarks>
        public const byte TestAddress = 0x7D;

        /// <summary>Command and reply identifier for the echo exchange.</summary>
        public const byte EchoCommandId = 0x01;

        /// <summary>Command and reply identifier for the baud rate change exchange.</summary>
        public const byte BaudRateChangeCommandId = 0x02;

        /// <summary>
        /// Number of bytes preceding the test data in an echo request or response: three vendor
        /// code bytes, command/reply identifier, sequence number, pattern or status, and length.
        /// </summary>
        public const int EchoHeaderLength = 7;

        /// <summary>
        /// The largest test payload every conforming responder must accept, 113 bytes.
        /// </summary>
        /// <remarks>
        /// OSDP section 5.6 guarantees only a 128-byte receive buffer. A frame carries 6 header
        /// bytes and 2 CRC bytes, leaving 120 bytes of message data, of which
        /// <see cref="EchoHeaderLength"/> are consumed by the line quality header. At this length
        /// both the command and its echoed reply are exactly 128 bytes.
        /// </remarks>
        public const int MaxPayloadLength = 113;

        /// <summary>
        /// The payload lengths exercised by the test matrix in section 3.10.
        /// </summary>
        public static IReadOnlyList<int> TestPayloadLengths { get; } = new[] { 0, 48, MaxPayloadLength };

        private static readonly int[] BaudRateValues =
            { 9600, 19200, 38400, 57600, 115200, 230400, 460800 };

        /// <summary>
        /// The six baud rates named by OSDP section 5.2, in ascending order. This is the default
        /// set a test sweeps; 460800 is deliberately excluded because it is not an OSDP rate.
        /// </summary>
        public static IReadOnlyList<int> DefaultBaudRates { get; } =
            new[] { 9600, 19200, 38400, 57600, 115200, 230400 };

        /// <summary>
        /// The reserved line quality vendor code, 02-00-0A.
        /// </summary>
        public static ReadOnlySpan<byte> VendorCode => VendorCodeBytes;

        /// <summary>
        /// Determines whether the supplied bytes are the reserved line quality vendor code.
        /// </summary>
        /// <param name="candidate">Bytes to test, normally the first three bytes of an osdp_MFG payload.</param>
        /// <returns><c>true</c> when the bytes match 02-00-0A.</returns>
        public static bool IsLineQualityVendorCode(ReadOnlySpan<byte> candidate)
        {
            if (candidate.Length < VendorCodeBytes.Length) return false;

            for (int index = 0; index < VendorCodeBytes.Length; index++)
            {
                if (candidate[index] != VendorCodeBytes[index]) return false;
            }

            return true;
        }

        /// <summary>
        /// Generates the test data for a pattern.
        /// </summary>
        /// <param name="pattern">The pattern to generate.</param>
        /// <param name="length">Number of bytes to generate, 0 to <see cref="MaxPayloadLength"/>.</param>
        /// <returns>The generated test data.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The length is negative or exceeds
        /// <see cref="MaxPayloadLength"/>, or the pattern is not defined.</exception>
        public static byte[] GeneratePattern(TestPattern pattern, int length)
        {
            if (length < 0 || length > MaxPayloadLength)
            {
                throw new ArgumentOutOfRangeException(nameof(length),
                    $"Payload length must be between 0 and {MaxPayloadLength}.");
            }

            if (!IsSupportedPattern(pattern))
            {
                throw new ArgumentOutOfRangeException(nameof(pattern), $"Unknown test pattern {pattern}.");
            }

            var data = new byte[length];
            for (int index = 0; index < length; index++)
            {
                data[index] = ExpectedByte(pattern, index);
            }

            return data;
        }

        /// <summary>
        /// Determines whether data matches what the pattern should have produced.
        /// </summary>
        /// <param name="pattern">The expected pattern.</param>
        /// <param name="data">The data received back from the responder.</param>
        /// <returns><c>true</c> when every byte matches the pattern.</returns>
        public static bool ValidatePattern(TestPattern pattern, ReadOnlySpan<byte> data)
        {
            if (!IsSupportedPattern(pattern)) return false;

            for (int index = 0; index < data.Length; index++)
            {
                if (data[index] != ExpectedByte(pattern, index)) return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether a pattern identifier is one this implementation can generate.
        /// </summary>
        /// <param name="pattern">The pattern identifier to test.</param>
        /// <returns><c>true</c> when the pattern is defined by the specification.</returns>
        /// <remarks>
        /// The underlying type is <see cref="byte"/>, so an identifier can never be below
        /// <see cref="TestPattern.AllZeros"/>; only the upper bound needs checking.
        /// </remarks>
        public static bool IsSupportedPattern(TestPattern pattern) => pattern <= TestPattern.WalkingOne;

        /// <summary>
        /// Converts a Baud Rate ID into the baud rate it represents.
        /// </summary>
        /// <param name="baudRateId">The identifier to convert.</param>
        /// <returns>The baud rate in bits per second.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The identifier is not defined.</exception>
        public static int ToBaudRate(LineQualityBaudRate baudRateId)
        {
            // The enum is byte-backed, so the index cannot be negative.
            int index = (int)baudRateId;
            if (index >= BaudRateValues.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(baudRateId), $"Unknown baud rate ID {baudRateId}.");
            }

            return BaudRateValues[index];
        }

        /// <summary>
        /// Converts a baud rate into the Baud Rate ID that represents it.
        /// </summary>
        /// <param name="baudRate">The baud rate in bits per second.</param>
        /// <param name="baudRateId">On success, the matching identifier.</param>
        /// <returns><c>true</c> when the rate has a defined identifier.</returns>
        public static bool TryGetBaudRateId(int baudRate, out LineQualityBaudRate baudRateId)
        {
            for (int index = 0; index < BaudRateValues.Length; index++)
            {
                if (BaudRateValues[index] != baudRate) continue;

                baudRateId = (LineQualityBaudRate)index;
                return true;
            }

            baudRateId = default;
            return false;
        }

        /// <summary>
        /// Returns the number of iterations a profile runs for each pattern and size combination.
        /// </summary>
        /// <param name="profile">The profile to look up.</param>
        /// <returns>Iterations per combination.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The profile is not defined.</exception>
        public static int IterationsPerCombination(TestProfile profile)
        {
            switch (profile)
            {
                case TestProfile.Screening: return 10;
                case TestProfile.Qualification: return 60;
                case TestProfile.Extended: return 200;
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile), $"Unknown test profile {profile}.");
            }
        }

        /// <summary>
        /// Calculates the smallest packet loss rate a run of the given size can rule out, as a
        /// percentage, using the rule of three: with no observed failures the 95% upper confidence
        /// bound on the loss rate is approximately 3/N.
        /// </summary>
        /// <param name="packetsSent">Number of packets sent at a single baud rate.</param>
        /// <returns>The detection limit as a percentage, or 100 when nothing was sent.</returns>
        /// <remarks>
        /// This is why a zero-failure result is not the same as a zero loss rate. Reporting the
        /// limit alongside a pass keeps the result from being read as a stronger claim than the
        /// sample size supports.
        /// </remarks>
        public static double DetectionLimitPercent(int packetsSent) =>
            packetsSent <= 0 ? 100.0 : 300.0 / packetsSent;

        /// <summary>
        /// Calculates how long a frame occupies the line, used to let a transmission drain before
        /// changing the baud rate.
        /// </summary>
        /// <param name="byteCount">Number of bytes in the frame.</param>
        /// <param name="baudRate">The rate the frame was sent at.</param>
        /// <returns>The transmission time, assuming ten bits per byte.</returns>
        public static TimeSpan TransmissionTime(int byteCount, int baudRate) =>
            baudRate <= 0
                ? TimeSpan.Zero
                : TimeSpan.FromSeconds(byteCount * 10.0 / baudRate);

        internal static byte ExpectedByte(TestPattern pattern, int index)
        {
            switch (pattern)
            {
                case TestPattern.AllZeros: return 0x00;
                case TestPattern.AllOnes: return 0xFF;
                case TestPattern.AlternatingA: return 0xAA;
                case TestPattern.Alternating5: return 0x55;
                case TestPattern.Sequential: return (byte)(index & 0xFF);
                case TestPattern.WalkingOne: return (byte)(1 << (index % 8));
                default: return 0x00;
            }
        }
    }
}
