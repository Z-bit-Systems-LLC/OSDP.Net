namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Data transfer object for device selection dialog input
    /// </summary>
    public class DeviceSelectionInput
    {
        public byte SelectedDeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}