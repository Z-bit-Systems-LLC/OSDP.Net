namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Data transfer object for remove device dialog input
    /// </summary>
    public class RemoveDeviceInput
    {
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}