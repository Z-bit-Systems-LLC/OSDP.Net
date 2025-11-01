namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for ACU receive size dialog input
    /// </summary>
    public class ACUReceiveSizeInput
    {
        public byte MaximumReceiveSize { get; set; }
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}
