using System;
using System.Collections.Generic;
using OSDP.Net.Messages;

namespace OSDP.Net.Model.CommandData
{
    /// <summary>
    /// Represents a fragment of data for a multipart message.
    /// </summary>
    public class MessageDataFragment
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageDataFragment"/> class.
        /// </summary>
        /// <param name="totalSize">Total message data size as little-endian format</param>
        /// <param name="offset">Offset of the current message</param>
        /// <param name="fragmentSize">Size of the fragment</param>
        /// <param name="dataFragment">Message fragment data</param>
        public MessageDataFragment(int totalSize, int offset, ushort fragmentSize, byte[] dataFragment)
        {
            TotalSize = totalSize;
            Offset = offset;
            FragmentSize = fragmentSize;
            DataFragment = dataFragment;
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
            var data = new List<byte>();
            data.AddRange(Message.ConvertIntToBytes(TotalSize));
            data.AddRange(Message.ConvertIntToBytes(Offset));
            data.AddRange(Message.ConvertShortToBytes(FragmentSize));
            data.AddRange(DataFragment);
            return data.ToArray();
        }

        /// <summary>Parses the message payload bytes</summary>
        /// <param name="data">Message payload as bytes</param>
        /// <returns>An instance of MessageDataFragment representing the message payload</returns>
        public static MessageDataFragment ParseData(ReadOnlySpan<byte> data)
        {
            return new MessageDataFragment(
                Message.ConvertBytesToInt(data.Slice(0, 4).ToArray()),
                Message.ConvertBytesToInt(data.Slice(4, 4).ToArray()),
                Message.ConvertBytesToUnsignedShort(data.Slice(8, 2).ToArray()),
                data.Slice(10).ToArray());
        }
    }
}