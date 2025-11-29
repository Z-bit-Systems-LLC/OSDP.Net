namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// Defines the tag types used in the extended device identification TLV stream.
    /// </summary>
    public enum ExtendedIdTag : byte
    {
        /// <summary>
        /// Manufacturer name in UTF-8 format.
        /// </summary>
        Manufacturer = 0,

        /// <summary>
        /// Product name in UTF-8 format.
        /// </summary>
        ProductName = 1,

        /// <summary>
        /// Serial number in UTF-8 format.
        /// </summary>
        SerialNumber = 2,

        /// <summary>
        /// Firmware version in UTF-8 format. Multiple entries allowed for different microcontrollers.
        /// </summary>
        FirmwareVersion = 3,

        /// <summary>
        /// Hardware description in UTF-8 format.
        /// </summary>
        HardwareDescription = 4,

        /// <summary>
        /// URL in UTF-8 format.
        /// </summary>
        Url = 5,

        /// <summary>
        /// Configuration reference in UTF-8 format.
        /// </summary>
        ConfigurationReference = 6
    }
}
