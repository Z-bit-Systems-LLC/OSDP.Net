using System;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

/// <summary>
/// Command data to control the buzzer on a PD.
/// </summary>
public class ReaderBuzzerControl : CommandData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReaderBuzzerControl"/> class.
    /// </summary>
    /// <param name="readerNumber">The reader number starting at 0.</param>
    /// <param name="toneCode">The tone code.</param>
    /// <param name="onTime">The on time in units of 100ms.</param>
    /// <param name="offTime">The off time in units of 100ms.</param>
    /// <param name="count">The number of times to repeat the on/off cycle.</param>
    public ReaderBuzzerControl(byte readerNumber, ToneCode toneCode, byte onTime, byte offTime, byte count)
    {
        ReaderNumber = readerNumber;
        ToneCode = toneCode;
        OnTime = onTime;
        OffTime = offTime;
        Count = count;
    }

    /// <summary>
    /// Gets the reader number starting at 0.
    /// </summary>
    public byte ReaderNumber { get; }

    /// <summary>
    /// Gets the tone code.
    /// </summary>
    public ToneCode ToneCode { get; }

    /// <summary>
    /// Gets the on time in units of 100ms.
    /// </summary>
    public byte OnTime { get; }

    /// <summary>
    /// Gets the off time in units of 100ms.
    /// </summary>
    public byte OffTime { get; }

    /// <summary>
    /// Gets the number of times to repeat the on/off cycle.
    /// </summary>
    public byte Count { get;  }

    /// <summary>Parses the message payload bytes</summary>
    /// <param name="data">Message payload as bytes</param>
    /// <returns>An instance of ReaderBuzzerControl representing the message payload</returns>
    public static ReaderBuzzerControl ParseData(ReadOnlySpan<byte> data)
    {
        return new ReaderBuzzerControl(
            data[0], 
            (ToneCode)data[1], 
            data[2], 
            data[3], 
            data[4]);
    }
 
    
    /// <inheritdoc />
    public override CommandType CommandType => CommandType.BuzzerControl;

    /// <inheritdoc />
    public override byte Code => (byte)CommandType;
        
    /// <inheritdoc />
    public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

    /// <inheritdoc />
    public override byte[] BuildData()
    {
        return [ReaderNumber, (byte) ToneCode, OnTime, OffTime, Count];
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($" Reader #: {ReaderNumber}");
        sb.AppendLine($"Tone Code: {ToneCode}");
        sb.AppendLine($"  On Time: {OnTime}");
        sb.AppendLine($" Off Time: {OffTime}");
        sb.AppendLine($"    Count: {Count}");
        return sb.ToString();
    }
}

/// <summary>
/// Tone codes values.
/// </summary>
public enum ToneCode
{
    [Obsolete("This no tone code is obsolete.", false)]
#pragma warning disable CS1591
    None = 0x00,
#pragma warning restore CS1591

    /// <summary>
    /// Turn off the tone
    /// </summary>
    Off = 0x01,

    /// <summary>
    /// Turn on the default tone
    /// </summary>
    Default = 0x02
}