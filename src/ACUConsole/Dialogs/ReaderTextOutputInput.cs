namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Data transfer object for reader text output dialog input
    /// </summary>
    public class ReaderTextOutputInput
    {
        public byte ReaderNumber { get; set; }
        public string Text { get; set; } = string.Empty;
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}