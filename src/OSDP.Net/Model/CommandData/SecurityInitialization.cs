using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

internal class SecurityInitialization : CommandData
{
    /// <inheritdoc />
    public SecurityInitialization(byte[] serverRandomNumber, bool isDefaultKey)
    {
        ServerRandomNumber = serverRandomNumber ?? throw new ArgumentNullException(nameof(serverRandomNumber));
        IsDefaultKey = isDefaultKey;
        Version = SecureChannelVersion.V1;
    }

    /// <summary>
    /// Creates a new SecurityInitialization for the specified secure channel version.
    /// </summary>
    /// <param name="serverRandomNumber">The server random number (8 bytes for SC1, 16 bytes for SC2).</param>
    /// <param name="version">The secure channel version.</param>
    public SecurityInitialization(byte[] serverRandomNumber, SecureChannelVersion version)
    {
        ServerRandomNumber = serverRandomNumber ?? throw new ArgumentNullException(nameof(serverRandomNumber));
        IsDefaultKey = false;
        Version = version;
    }

    /// <summary>
    /// The server random number (RndA).
    /// </summary>
    public byte[] ServerRandomNumber { get; }

    /// <summary>
    /// Whether the default key is being used (SC1 only).
    /// </summary>
    public bool IsDefaultKey { get; }

    /// <summary>
    /// The secure channel version.
    /// </summary>
    public SecureChannelVersion Version { get; }

    /// <inheritdoc />
    public override CommandType CommandType => CommandType.SessionChallenge;

    /// <inheritdoc />
    public override byte Code => (byte)CommandType;

    /// <inheritdoc />
    public override bool IsSecurityInitialization => true;

    /// <inheritdoc />
    public override ReadOnlySpan<byte> SecurityControlBlock()
    {
        byte scbData = Version == SecureChannelVersion.V2
            ? (byte)0x02
            : (byte)(IsDefaultKey ? 0x00 : 0x01);

        return new byte[]
        {
            0x03,
            (byte)SecurityBlockType.BeginNewSecureConnectionSequence,
            scbData
        };
    }

    /// <inheritdoc />
    public override byte[] BuildData()
    {
        return ServerRandomNumber;
    }

    /// <summary>
    /// Parses the message payload bytes
    /// </summary>
    /// <param name="data">Message payload as bytes</param>
    /// <param name="securityControlBlock">Security control block as bytes</param>
    /// <returns>An instance of SecurityInitialization representing the message payload</returns>
    public static SecurityInitialization ParseData(ReadOnlySpan<byte> data, ReadOnlySpan<byte> securityControlBlock)
    {
        if (securityControlBlock[2] == 0x02)
        {
            return new SecurityInitialization(data.ToArray(), SecureChannelVersion.V2);
        }

        return new SecurityInitialization(data.ToArray(), securityControlBlock[2] == 0x01);
    }
}