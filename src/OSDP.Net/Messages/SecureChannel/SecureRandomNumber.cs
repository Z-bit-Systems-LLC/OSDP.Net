using Org.BouncyCastle.Security;

namespace OSDP.Net.Messages.SecureChannel;

/// <summary>
/// Cryptographic random number source for the secure channel handshake random numbers
/// (RND.A on the ACU, RND.B on the PD). Their freshness is what stops a recorded handshake
/// from being replayed, so they must not come from <see cref="System.Random"/>.
/// </summary>
internal static class SecureRandomNumber
{
    // SecureRandom is thread-safe, so a single instance is shared
    private static readonly SecureRandom Generator = new();

    /// <summary>
    /// Fills the buffer with cryptographically strong random bytes.
    /// </summary>
    /// <param name="buffer">The buffer to fill.</param>
    internal static void Fill(byte[] buffer) => Generator.NextBytes(buffer);
}
