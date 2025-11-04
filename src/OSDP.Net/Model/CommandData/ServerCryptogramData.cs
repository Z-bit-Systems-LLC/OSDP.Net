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
    
    /// <inheritdoc />
    public override CommandType CommandType => CommandType.ServerCryptogram;

    /// <inheritdoc />
    public override byte Code => (byte)CommandType;

    /// <inheritdoc />
    public override bool IsSecurityInitialization => true;

    /// <inheritdoc />
    public override ReadOnlySpan<byte> SecurityControlBlock()
    {
        return new byte[]
        {
            0x03,
            (byte)SecurityBlockType.SecureConnectionSequenceStep3,
            (byte)(IsDefaultKey ? 0x00 : 0x01)
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