using System;
using System.Linq;
using System.Text;

using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.ReplyData;

/// <summary>
/// A formated card data reply.
/// </summary>
public class FormattedCardData : PayloadData
{
    /// <summary>
    /// Creates a new instance of FormattedCardData. The parameters passed here are
    /// defined in OSDP spec for osdp_FMT response
    /// </summary>
    /// <param name="readerNumber">Reader number</param>
    /// <param name="direction">Read direction</param>
    /// <param name="data">Data</param>
    public FormattedCardData(byte readerNumber, ReadDirection direction, string data)
    {
        ReaderNumber = readerNumber;
        ReadDirection = direction;
        Data = data;
        Lenght = (ushort)data.Length;
    }

    /// <summary>
    /// The reader number.
    /// </summary>
    public byte ReaderNumber { get; }

    /// <summary>
    /// The direction for reading the formatted card data.
    /// </summary>
    public ReadDirection ReadDirection { get; }

    /// <summary>
    /// The lenght of the data.
    /// </summary>
    public ushort Lenght { get; }

    /// <summary>
    /// The formatted card data.
    /// </summary>
    public string Data { get; }

    /// <inheritdoc/>
    public override byte Code => (byte)ReplyType.FormattedReaderData;

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> SecurityControlBlock()
    {
        return SecurityBlock.ReplyMessageWithDataSecurity;
    }

    /// <summary>
    /// Parses the data.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>FormattedCardData.</returns>
    public static FormattedCardData ParseData(ReadOnlySpan<byte> data)
    {
        var dataArray = data.ToArray();
        var readerNumber = data[0];
        var reverse = (data[1] & 0x01) == 1;
        var numberOfBytes = data[2];

        if (reverse)
        {
            dataArray = dataArray.Skip(3).Take(numberOfBytes).Reverse().ToArray();
        }
        else
        {
            dataArray = dataArray.Skip(3).Take(numberOfBytes).ToArray();
        }

        var cardData = Encoding.ASCII.GetString(dataArray);

        return new FormattedCardData(readerNumber, (ReadDirection)data[1], cardData);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var build = new StringBuilder();
        build.AppendLine($" Reader Number: {ReaderNumber}");
        build.AppendLine($"Read Direction: {ReadDirection}");
        build.AppendLine($"   Data Length: {Lenght}");
        build.AppendLine($"          Data: {Data}");
        return build.ToString();
    }

    /// <inheritdoc/>
    public override byte[] BuildData()
    {
        var lenght = 3 + Data.Length;
        var buffer = new byte[lenght];
        buffer[0] = ReaderNumber;
        buffer[1] = (byte)ReadDirection;
        buffer[2] = (byte)Data.Length;
        Encoding.ASCII.GetBytes(Data).CopyTo(buffer, 3);

        return buffer;
    }
}

/// <summary>
/// The direction for reading the formatted card data.
/// </summary>
public enum ReadDirection
{
    /// <summary>
    /// Forward direction.
    /// </summary>
    Forward = 0x00,
    /// <summary>
    /// Reverse direction.
    /// </summary>
    Reverse = 0x01
}
