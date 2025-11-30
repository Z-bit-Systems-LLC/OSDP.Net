namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// Represents the possible states of an input as defined in Table 52 of the OSDP v2.2.2 specification.
    /// </summary>
    public enum InputStatusValue : byte
    {
        /// <summary>
        /// Input is in its normal state (e.g., door closed, window unbroken, or no motion detected).
        /// </summary>
        Inactive = 0x00,

        /// <summary>
        /// Input is of concern (e.g., door ajar, window broken, or motion detected).
        /// </summary>
        Active = 0x01,

        /// <summary>
        /// Short circuit or no resistance detected (e.g., wires crossed or jumper on switch).
        /// </summary>
        Short = 0x02,

        /// <summary>
        /// Open circuit or infinite resistance (e.g., wires break or are cut).
        /// </summary>
        Open = 0x03,

        /// <summary>
        /// Resistance doesn't correspond to known state (e.g., wires being tampered with or circuit defective).
        /// </summary>
        Fault = 0x04,

        /// <summary>
        /// Not initialized or needs configuration (e.g., input has not met de-bounce requirements).
        /// </summary>
        Unknown = 0x05

        // Note: Values 0x06-0x7F are reserved for future use
        // Note: Values 0x80-0xFF are reserved for private/vendor-defined use
    }
}
