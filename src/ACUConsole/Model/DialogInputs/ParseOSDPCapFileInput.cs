namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Input data for Parse OSDP Cap File dialog
    /// </summary>
    public class ParseOSDPCapFileInput
    {
        public string FilePath { get; set; } = string.Empty;
        public byte? FilterAddress { get; set; }
        public bool IgnorePollsAndAcks { get; set; }
        public byte[] SecureKey { get; set; } = [];
        public bool WasCancelled { get; set; }
    }
}