namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for communication configuration dialog input
    /// </summary>
    public class CommunicationConfigurationInput
    {
        public byte NewAddress { get; set; }
        public int NewBaudRate { get; set; }
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}