namespace PDConsole.Model.DialogInputs
{
    /// <summary>
    /// Input model for serial connection configuration dialog
    /// </summary>
    public class SerialConnectionInput
    {
        /// <summary>
        /// The selected COM port name
        /// </summary>
        public string PortName { get; set; } = string.Empty;

        /// <summary>
        /// The selected baud rate
        /// </summary>
        public int BaudRate { get; set; }

        /// <summary>
        /// Whether the dialog was cancelled
        /// </summary>
        public bool WasCancelled { get; set; }
    }
}