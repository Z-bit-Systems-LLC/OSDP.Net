using System;
using System.Linq;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

/// <summary>
/// Command data to set the communication configuration on a PD.
/// </summary>
public class CommunicationConfiguration : CommandData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommunicationConfiguration"/> class.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="baudRate">The baud rate.</param>
    public CommunicationConfiguration(byte address, int baudRate)
    {
        Address = address;
        BaudRate = baudRate;
    }

    /// <summary>
    /// Gets the address.
    /// </summary>
    public byte Address { get; }

    /// <summary>
    /// Gets the baud rate.
    /// </summary>
    public int BaudRate { get; }
    
    /// <inheritdoc />
    public override CommandType CommandType => CommandType.CommunicationSet;

    /// <inheritdoc />
    public override byte Code => (byte)CommandType;
        
    /// <inheritdoc />
    public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

    /// <inheritdoc />
    public override byte[] BuildData()
    {
        var baudRateBytes = Message.ConvertIntToBytes(BaudRate).ToArray();
            
        return new[]
        {
            Address,
            baudRateBytes[0],
            baudRateBytes[1],
            baudRateBytes[2],
            baudRateBytes[3]
        };
    }

    /// <summary>Parses the message payload bytes</summary>
    /// <param name="data">Message payload as bytes</param>
    /// <returns>An instance of CommunicationConfiguration representing the message payload</returns>
    public static CommunicationConfiguration ParseData(ReadOnlySpan<byte> data)
    {
        return new CommunicationConfiguration(data[0], Message.ConvertBytesToInt(data.Slice(1, 4).ToArray()));
    }
    
    /// <inheritdoc />
    public override string ToString()
    {
        var build = new StringBuilder();
        build.AppendLine($"  Address: {Address}");
        build.AppendLine($"Baud Rate: {BaudRate}");

        return build.ToString();
    }
}