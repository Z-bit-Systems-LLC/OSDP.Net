using OSDP.Net.Messages.SecureChannel;
using PDConsole.Configuration;

namespace PDConsole.Model.DialogInputs
{
    /// <summary>
    /// Input model for the activate-device dialog, capturing both the serial connection and the full
    /// set of secure channel options (including asymmetric pairing).
    /// </summary>
    public class ActivateDeviceInput
    {
        /// <summary>The selected COM port name.</summary>
        public string PortName { get; set; } = string.Empty;

        /// <summary>The selected baud rate.</summary>
        public int BaudRate { get; set; }

        /// <summary>The chosen secure channel operating mode.</summary>
        public SecureChannelMode SecureChannelMode { get; set; } = SecureChannelMode.ClearText;

        /// <summary>The secure channel protocol version (used by Install and Secure modes).</summary>
        public SecureChannelVersion SecureChannelVersion { get; set; } = SecureChannelVersion.V1;

        /// <summary>The secure channel base key (SCBK) as a hex string, used by Secure mode.</summary>
        public string SecureChannelKey { get; set; } = string.Empty;

        /// <summary>Whether pairing uses the built-in reproducible demonstration certificate authority.</summary>
        public bool UseDemoCa { get; set; } = true;

        /// <summary>Optional 32-byte ML-DSA device seed (hex) for a reproducible pairing identity.</summary>
        public string DeviceSeedHex { get; set; } = string.Empty;

        /// <summary>Whether the dialog was cancelled.</summary>
        public bool WasCancelled { get; set; } = true;
    }
}
