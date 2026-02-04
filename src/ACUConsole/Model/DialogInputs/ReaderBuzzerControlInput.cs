using OSDP.Net.Model.CommandData;

namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for reader buzzer control dialog input with full OSDP 2.2.2 support
    /// </summary>
    public class ReaderBuzzerControlInput
    {
        public bool WasCancelled { get; set; }
        public byte DeviceAddress { get; set; }
        public byte ReaderNumber { get; set; }
        public ToneCode ToneCode { get; set; }
        public byte OnTime { get; set; }
        public byte OffTime { get; set; }
        public byte Count { get; set; }
    }
}
