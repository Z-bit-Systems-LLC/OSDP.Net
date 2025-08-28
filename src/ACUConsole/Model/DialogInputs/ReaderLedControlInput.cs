using OSDP.Net.Model.CommandData;

namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for reader LED control dialog input
    /// </summary>
    public class ReaderLedControlInput
    {
        public byte LedNumber { get; set; }
        public LedColor Color { get; set; }
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}