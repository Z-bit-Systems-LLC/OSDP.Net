using OSDP.Net.Model.CommandData;

namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for reader LED control dialog input
    /// </summary>
    public class ReaderLedControlInput
    {
        /// <summary>
        /// Indicates if the dialog was cancelled
        /// </summary>
        public bool WasCancelled { get; set; }

        /// <summary>
        /// The device address to send the command to
        /// </summary>
        public byte DeviceAddress { get; set; }

        /// <summary>
        /// The reader number (0-based)
        /// </summary>
        public byte ReaderNumber { get; set; }

        /// <summary>
        /// The LED number on the reader
        /// </summary>
        public byte LedNumber { get; set; }

        // Temporary settings

        /// <summary>
        /// The temporary control mode
        /// </summary>
        public TemporaryReaderControlCode TemporaryMode { get; set; }

        /// <summary>
        /// Temporary ON time in units of 100ms
        /// </summary>
        public byte TemporaryOnTime { get; set; }

        /// <summary>
        /// Temporary OFF time in units of 100ms
        /// </summary>
        public byte TemporaryOffTime { get; set; }

        /// <summary>
        /// Temporary ON color (byte value to support custom colors 0x00-0xFF)
        /// </summary>
        public byte TemporaryOnColor { get; set; }

        /// <summary>
        /// Temporary OFF color (byte value to support custom colors 0x00-0xFF)
        /// </summary>
        public byte TemporaryOffColor { get; set; }

        /// <summary>
        /// Temporary timer in units of 100ms (0 = forever)
        /// </summary>
        public ushort TemporaryTimer { get; set; }

        // Permanent settings

        /// <summary>
        /// The permanent control mode
        /// </summary>
        public PermanentReaderControlCode PermanentMode { get; set; }

        /// <summary>
        /// Permanent ON time in units of 100ms
        /// </summary>
        public byte PermanentOnTime { get; set; }

        /// <summary>
        /// Permanent OFF time in units of 100ms
        /// </summary>
        public byte PermanentOffTime { get; set; }

        /// <summary>
        /// Permanent ON color (byte value to support custom colors 0x00-0xFF)
        /// </summary>
        public byte PermanentOnColor { get; set; }

        /// <summary>
        /// Permanent OFF color (byte value to support custom colors 0x00-0xFF)
        /// </summary>
        public byte PermanentOffColor { get; set; }
    }
}
