namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for the asymmetric pairing device-selection dialog.
    /// </summary>
    public class PairDeviceInput
    {
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}
