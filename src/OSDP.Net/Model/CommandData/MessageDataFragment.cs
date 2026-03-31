using System;
using System.Collections.Generic;
using OSDP.Net.Messages;

namespace OSDP.Net.Model.CommandData
{
    /// <summary>
    /// Defines the field size used for the <see cref="MessageDataFragment.TotalSize"/> and
    /// <see cref="MessageDataFragment.Offset"/> values.
    /// </summary>
    public enum MessageDataFragmentFieldSize
    {
        /// <summary>
        /// Use 2-byte unsigned integer fields.
        /// </summary>
        TwoBytes,

        /// <summary>
        /// Use 4-byte signed integer fields.
        /// </summary>
        FourBytes
    }

    /// <summary>
    /// Represents a fragment of data for a multipart message.
    /// </summary>
    public class MessageDataFragment
    {
        private readonly MessageDataFragmentFieldSize sizeAndOffsetFieldSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageDataFragment"/> class.
        /// </summary>
        /// <param name="totalSize">Total message data size as little-endian format</param>
        /// <param name="offset">Offset of the current message</param>
        /// <param name="fragmentSize">Size of the fragment</param>
        /// <param name="dataFragment">Message fragment data</param>
        /// <param name="sizeAndOffsetFieldSize">The field size used for <see cref="TotalSize"/> and <see cref="Offset"/>.</param>
        public MessageDataFragment(int totalSize, int offset, ushort fragmentSize, byte[] dataFragment,
            MessageDataFragmentFieldSize sizeAndOffsetFieldSize)
        {
            TotalSize = totalSize;
            Offset = offset;
            FragmentSize = fragmentSize;
            DataFragment = dataFragment;
            this.sizeAndOffsetFieldSize = sizeAndOffsetFieldSize;
        }

        /// <summary>
        /// Get the total message data size as little-endian format
        /// </summary>
        public int TotalSize { get; }

        /// <summary>
        /// Get the offset of the current message
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// Get the size of the fragment
        /// </summary>
        public ushort FragmentSize { get; }

        /// <summary>
        /// Get the fragment data
        /// </summary>
        public byte[] DataFragment { get; }

        internal ReadOnlySpan<byte> BuildData()
        {
            var useTwoByteFields = sizeAndOffsetFieldSize == MessageDataFragmentFieldSize.TwoBytes;
            var data = new List<byte>();
            data.AddRange(useTwoByteFields ? Message.ConvertShortToBytes((ushort)TotalSize) : Message.ConvertIntToBytes(TotalSize));
            data.AddRange(useTwoByteFields ? Message.ConvertShortToBytes((ushort)Offset) : Message.ConvertIntToBytes(Offset));
            data.AddRange(Message.ConvertShortToBytes(FragmentSize));
            data.AddRange(DataFragment);
            return data.ToArray();
        }

        /// <summary>Parses the message payload bytes</summary>
        /// <param name="data">Message payload as bytes</param>
        /// <param name="sizeAndOffsetFieldSize">The field size used for <see cref="TotalSize"/> and <see cref="Offset"/>.</param>
        /// <returns>An instance of MessageDataFragment representing the message payload</returns>
        public static MessageDataFragment ParseData(ReadOnlySpan<byte> data,
            MessageDataFragmentFieldSize sizeAndOffsetFieldSize)
        {
            var dataOffset = 0;

            var totalSize = ReadValue(sizeAndOffsetFieldSize, data, ref dataOffset);
            var offset = ReadValue(sizeAndOffsetFieldSize, data, ref dataOffset);
            var fragmentSize = (ushort)ReadValue(MessageDataFragmentFieldSize.TwoBytes, data, ref dataOffset);
            var fragmentData = data.Slice(dataOffset).ToArray();

            return new MessageDataFragment(
               totalSize,
               offset,
               fragmentSize,
               fragmentData,
               sizeAndOffsetFieldSize);

            static int ReadValue(MessageDataFragmentFieldSize fieldSize, ReadOnlySpan<byte> data, ref int offset)
            {
                switch (fieldSize)
                {
                    case MessageDataFragmentFieldSize.TwoBytes:
                        var twoByteValue = Message.ConvertBytesToUnsignedShort(data.Slice(offset, 2));
                        offset += 2;
                        return twoByteValue;
                    case MessageDataFragmentFieldSize.FourBytes:
                        var fourByteValue = Message.ConvertBytesToInt(data.Slice(offset, 4).ToArray());
                        offset += 4;
                        return fourByteValue;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(fieldSize), fieldSize, null);
                }
            }
        }
    }
}