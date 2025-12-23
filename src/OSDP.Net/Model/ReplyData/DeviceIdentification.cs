using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// The PD identification data sent as a reply.
    /// </summary>
    public class DeviceIdentification : PayloadData
    {
        /// <summary>
        /// Creates a new instance of DeviceIdentification
        /// </summary>
        public DeviceIdentification(byte[] vendorCode, byte modelNumber, byte version, int serialNumber,
            byte firmwareMajor, byte firmwareMinor, byte firmwareBuild)
        {
            VendorCode = vendorCode;
            ModelNumber = modelNumber;
            Version = version;
            SerialNumber = serialNumber;
            FirmwareMajor = firmwareMajor;
            FirmwareMinor = firmwareMinor;
            FirmwareBuild = firmwareBuild;
        }

        /// <summary>Gets the vendor code.</summary>
        public IEnumerable<byte> VendorCode { get; }

        /// <summary>Gets the model number.</summary>
        public byte ModelNumber { get;  }

        /// <summary>Gets the hardware version.</summary>
        public byte Version { get; }

        /// <summary>Gets the serial number.</summary>
        public int SerialNumber { get; }

        /// <summary>
        /// Gets the firmware major version.
        /// </summary>
        public byte FirmwareMajor { get; }

        /// <summary>
        /// Gets the firmware minor version.
        /// </summary>
        public byte FirmwareMinor { get; }

        /// <summary>
        /// Gets the firmware build.
        /// </summary>
        public byte FirmwareBuild { get; }
        
        /// <inheritdoc/>
        public override byte Code => (byte)ReplyType.PdIdReport;
        
        /// <inheritdoc/>
        public override ReadOnlySpan<byte> SecurityControlBlock()
        {
            return SecurityBlock.ReplyMessageWithDataSecurity;
        }

        /// <summary>Parses the message payload bytes</summary>
        /// <param name="data">Message payload as bytes</param>
        /// <returns>An instance of DeviceIdentification representing the message payload</returns>
        public static DeviceIdentification ParseData(ReadOnlySpan<byte> data)
        {
            var dataArray = data.ToArray();
            if (dataArray.Length != 12)
            {
                throw new Exception("Invalid size for the data");
            }

            var deviceIdentification = new DeviceIdentification(dataArray.Take(3).ToArray(), dataArray[3], dataArray[4],
                Message.ConvertBytesToInt(dataArray.Skip(5).Take(4).ToArray()), dataArray[9], dataArray[10],
                dataArray[11]);

            return deviceIdentification;
        }

        /// <inheritdoc/>
        public override byte[] BuildData()
        {
            var buffer = new byte[12];

            VendorCode.Take(3).ToArray().CopyTo(buffer, 0);
            buffer[3] = ModelNumber;
            buffer[4] = Version;
            Message.ConvertIntToBytes(SerialNumber).ToArray().CopyTo(buffer, 5);
            buffer[9] = FirmwareMajor;
            buffer[10] = FirmwareMinor;
            buffer[11] = FirmwareBuild;

            return buffer;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var build = new StringBuilder();
            build.AppendLine($"     Vendor Code: {BitConverter.ToString(VendorCode.ToArray())}");
            build.AppendLine($"    Model Number: {ModelNumber}");
            build.AppendLine($"         Version: {Version}");
            build.AppendLine($"   Serial Number: {BitConverter.ToString(Message.ConvertIntToBytes(SerialNumber).ToArray())}");
            build.AppendLine($"Firmware Version: {FirmwareMajor}.{FirmwareMinor}.{FirmwareBuild}");

            return build.ToString();
        }
    }
}