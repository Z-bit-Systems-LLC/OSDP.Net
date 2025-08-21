namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Data transfer object for output control dialog input
    /// </summary>
    public class OutputControlInput
    {
        public byte OutputNumber { get; set; }
        public bool ActivateOutput { get; set; }
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}