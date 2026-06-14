using OSDP.Net.Model.CommandData;

namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for reader text output dialog input
    /// </summary>
    public class ReaderTextOutputInput
    {
        public byte ReaderNumber { get; set; }
        public TextCommand TextCommand { get; set; } = TextCommand.PermanentTextNoWrap;
        public byte TemporaryTextTime { get; set; }
        public byte Row { get; set; } = 1;
        public byte Column { get; set; } = 1;
        public string Text { get; set; } = string.Empty;
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}
