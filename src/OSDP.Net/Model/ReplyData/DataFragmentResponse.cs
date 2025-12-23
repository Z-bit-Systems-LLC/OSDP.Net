using System;
using System.Linq;
using System.Text;
using OSDP.Net.Messages;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// A multi-part message data fragment reply.
    /// </summary>
    public class DataFragmentResponse
    {
        private DataFragmentResponse()
        {
        }

        /// <summary>
        /// The length of the whole message.
        /// </summary>
        public ushort WholeMessageLength { get; private set; }

        /// <summary>
        /// The offset.
        /// </summary>
        public ushort Offset { get; private set; }

        /// <summary>
        /// The length of fragment.
        /// </summary>
        public ushort LengthOfFragment { get; private set; }

        /// <summary>
        /// The data.
        /// </summary>
        public byte[] Data { get; private set; }

        /// <summary>Parses the message payload bytes</summary>
        /// <param name="data">Message payload as bytes</param>
        /// <returns>An instance of DataFragmentResponse representing the message payload</returns>
        public static DataFragmentResponse ParseData(ReadOnlySpan<byte> data)
        {
            if (data.Length < 6)
            {
                // throw new Exception("Invalid size for the data");
                return new DataFragmentResponse {Data = new byte[] { }};
            }

            var fragmentResponse = new DataFragmentResponse
            {
                WholeMessageLength = Message.ConvertBytesToUnsignedShort(data.Slice(0, 2)),
                Offset = Message.ConvertBytesToUnsignedShort(data.Slice(2, 2)),
                LengthOfFragment = Message.ConvertBytesToUnsignedShort(data.Slice(4, 2)),
                Data = data.Slice(6, data.Length - 6).ToArray()
            };

            return fragmentResponse;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var build = new StringBuilder();
            build.AppendLine($"Whole Message Length: {WholeMessageLength}");
            build.AppendLine($"              Offset: {Offset}");
            build.AppendLine($"  Length of Fragment: {LengthOfFragment}");
            build.AppendLine($"                Data: {BitConverter.ToString(Data.ToArray())}");
            return build.ToString();
        }
    }
}
