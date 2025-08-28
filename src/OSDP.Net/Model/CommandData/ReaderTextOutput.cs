using System;
using System.Collections.Generic;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData
{
    /// <summary>
    /// Command data for sending text to be shown on a PD.
    /// </summary>
    public class ReaderTextOutput : CommandData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReaderTextOutput"/> class.
        /// </summary>
        /// <param name="readerNumber">The reader number starting at 0.</param>
        /// <param name="textCommand">The text command for displaying.</param>
        /// <param name="temporaryTextTime">The temporary text time in units of 100ms.</param>
        /// <param name="row">The row where the first character will be displayed. 0x01 = top row.</param>
        /// <param name="column">The column where the first character will be displayed 0x01 = leftmost column.</param>
        /// <param name="text">The text to display.</param>
        public ReaderTextOutput(byte readerNumber, TextCommand textCommand, byte temporaryTextTime, byte row, byte column, string text)
        {
            ReaderNumber = readerNumber;
            TextCommand = textCommand;
            TemporaryTextTime = temporaryTextTime;
            Row = row;
            Column = column;
            Text = text;
        }

        /// <summary>
        /// Gets the reader number starting at 0.
        /// </summary>
        public byte ReaderNumber { get; }

        /// <summary>
        /// Gets the text command for displaying.
        /// </summary>
        public TextCommand TextCommand { get; }

        /// <summary>
        /// Gets the temporary text time in units of 100ms.
        /// </summary>
        public byte TemporaryTextTime { get; }

        /// <summary>Gets the row where the first character will be displayed. 0x01 = top row.</summary>
        public byte Row { get; }

        /// <summary>The column where the first character will be displayed 0x01 = leftmost column.</summary>
        public byte Column { get; }

        /// <summary>
        /// Gets the text to display.
        /// </summary>
        public string Text { get; }

        /// <summary>Parses the message payload bytes</summary>
        /// <param name="data">Message payload as bytes</param>
        /// <returns>An instance of ReaderTextOutput representing the message payload</returns>
        public static ReaderTextOutput ParseData(ReadOnlySpan<byte> data)
        {
            string text = Encoding.ASCII.GetString(data.Slice(6).ToArray());
            return new ReaderTextOutput(data[0], (TextCommand)data[1], data[2], data[3], data[4], text);
        }

        /// <inheritdoc />
        public override CommandType CommandType => CommandType.TextOutput;

        /// <inheritdoc />
        public override byte Code => (byte)CommandType;
        
        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock()
        {
            return SecurityBlock.CommandMessageWithDataSecurity;
        }

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            var data = new List<byte>
                {ReaderNumber, (byte) TextCommand, TemporaryTextTime, Row, Column, (byte) Text.Length};
            data.AddRange(Encoding.ASCII.GetBytes(Text.Substring(0, Math.Min(Text.Length, byte.MaxValue))));
            return data.ToArray();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Reader Number: {ReaderNumber}");
            sb.AppendLine($"  Text Command: {TextCommand}");
            sb.AppendLine($"Temp Text Time: {TemporaryTextTime}");
            sb.AppendLine($"   Row, Column: {Row}, {Column}");
            sb.AppendLine($"  Display Text: {Text}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Text command for displaying values.
    /// </summary>
    public enum TextCommand
    {
        /// <summary>
        /// Display text permanently with no wrap
        /// </summary>
        PermanentTextNoWrap = 0x01,

        /// <summary>
        /// Display text permanently with wrap
        /// </summary>
        PermanentTextWithWrap = 0x02,

        /// <summary>
        /// Display text temporarily with no wrap
        /// </summary>
        TemporaryTextNoWrap = 0x03,

        /// <summary>
        /// Display text temporarily with wrap
        /// </summary>
        TemporaryTextWithWrap = 0x04
    }
}