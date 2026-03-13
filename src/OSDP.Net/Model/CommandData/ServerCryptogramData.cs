using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

/// <summary>
/// Represents the server cryptogram data used in a secure communication sequence.
/// Encapsulates information such as the server cryptogram and whether a default key is being used.
/// </summary>
public class ServerCryptogramData : CommandData
{
    /// <inheritdoc />
    public ServerCryptogramData(byte[] serverCryptogram, bool isDefaultKey)
    {
        ServerCryptogram = serverCryptogram ?? throw new ArgumentNullException(nameof(serverCryptogram));
        IsDefaultKey = isDefaultKey;
        Version = SecureChannelVersion.V1;
    }

    /// <summary>
    /// Creates a new ServerCryptogramData for the specified secure channel version.
    /// </summary>
    /// <param name="serverCryptogram">The server cryptogram (16 bytes for SC1, 32 bytes for SC2).</param>
    /// <param name="version">The secure channel version.</param>
    public ServerCryptogramData(byte[] serverCryptogram, SecureChannelVersion version)
    {
        ServerCryptogram = serverCryptogram ?? throw new ArgumentNullException(nameof(serverCryptogram));
        IsDefaultKey = false;
        Version = version;
    }

    /// <summary>
    /// Gets the cryptographic data provided by the server during a secure communication exchange.
    /// This property holds the server's cryptogram used for encrypting or authenticating messages.
    /// </summary>
    public byte[] ServerCryptogram { get; }

    /// <summary>
    /// Indicates whether the default encryption key is used for secure communications.
    /// This property specifies if the default key is applied during the security initialization process.
    /// </summary>
    public bool IsDefaultKey { get; }

    /// <summary>
    /// The secure channel version.
    /// </summary>
    public SecureChannelVersion Version { get; }

    /// <inheritdoc />
    public override CommandType CommandType => CommandType.ServerCryptogram;

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
            (byte)SecurityBlockType.SecureConnectionSequenceStep3,
            scbData
        };
    }

    /// <inheritdoc />
    public override byte[] BuildData()
    {
        return ServerCryptogram;
    }

    /// <summary>
    /// Parses the message payload bytes
    /// </summary>
    /// <param name="data">Message payload as bytes</param>
    /// <param name="securityControlBlock">Security control block as bytes</param>
    /// <returns>An instance of ServerCryptogram representing the message payload</returns>
    public static ServerCryptogramData ParseData(ReadOnlySpan<byte> data, ReadOnlySpan<byte> securityControlBlock)
    {
        return new ServerCryptogramData(data.ToArray(), securityControlBlock[2] == 0x01);
    }
}