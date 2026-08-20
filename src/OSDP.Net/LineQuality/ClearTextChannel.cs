using System;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.LineQuality
{
    /// <summary>
    /// A message channel that never establishes security, used to frame and parse line quality
    /// test traffic.
    /// </summary>
    /// <remarks>
    /// The Line Quality Test Procedure requires clear text, because a secure channel would encrypt
    /// the payload and turn every test pattern into statistically identical pseudo-random data.
    /// Because security is never established, the message building and parsing code never reaches
    /// the cryptographic members, so they throw rather than pretending to work.
    /// </remarks>
    internal sealed class ClearTextChannel : MessageSecureChannel
    {
        private const string NotSecuredMessage =
            "Line quality test traffic is never secured; this channel has no cryptographic state.";

        /// <summary>
        /// The shared instance. Safe to share because the channel holds no mutable state: security
        /// is never established, so nothing ever writes to the underlying security context.
        /// </summary>
        public static readonly ClearTextChannel Instance = new();

        private ClearTextChannel()
        {
        }

        public override byte[] DecodePayload(byte[] payload) => throw new NotSupportedException(NotSecuredMessage);

        public override void EncodePayload(byte[] payload, Span<byte> destination) =>
            throw new NotSupportedException(NotSecuredMessage);

        public override ReadOnlySpan<byte> GenerateMac(ReadOnlySpan<byte> message, bool isIncoming) =>
            throw new NotSupportedException(NotSecuredMessage);
    }
}
