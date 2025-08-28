using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData
{
    /// <summary>
    /// Manufacture specific command data sent to a PD.
    /// </summary>
    public class ManufacturerSpecific : CommandData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ManufacturerSpecific"/> class.
        /// </summary>
        /// <param name="vendorCode">The vendor code.</param>
        /// <param name="data">The manufacture specific data.</param>
        /// <exception cref="ArgumentNullException">vendorCode</exception>
        /// <exception cref="ArgumentNullException">data</exception>
        /// <exception cref="ArgumentOutOfRangeException">vendorCode</exception>
        public ManufacturerSpecific(byte[] vendorCode, byte[] data)
        {
            VendorCode = vendorCode ?? throw new ArgumentNullException(nameof(vendorCode));
            if (VendorCode.Length != 3) throw new ArgumentOutOfRangeException(nameof(vendorCode));

            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// Gets the vendor code.
        /// </summary>
        public byte[] VendorCode { get; }

        /// <summary>
        /// Gets the manufacture specific data.
        /// </summary>
        public byte[] Data { get; }
        
        /// <inheritdoc />
        public override CommandType CommandType => CommandType.ManufacturerSpecific;

        /// <inheritdoc />
        public override byte Code => (byte)CommandType;
        
        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            var data = new List<byte>
            {
                VendorCode[0],
                VendorCode[1],
                VendorCode[2]
            };
            data.AddRange(Data);
            return data.ToArray();
        }

        /// <summary>Parses the message payload bytes</summary>
        /// <param name="data">Message payload as bytes</param>
        /// <returns>An instance of ManufacturerSpecific representing the message payload</returns>
        public static ManufacturerSpecific ParseData(ReadOnlySpan<byte> data)
        {
            return new ManufacturerSpecific(
                data.Slice(0, 3).ToArray(),
                data.Slice(3).ToArray()
            );
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Vendor Code: {BitConverter.ToString(VendorCode.ToArray())}");
            sb.AppendLine($"       Data: {BitConverter.ToString(Data.ToArray())}");
            return sb.ToString();
        }
    }
}
