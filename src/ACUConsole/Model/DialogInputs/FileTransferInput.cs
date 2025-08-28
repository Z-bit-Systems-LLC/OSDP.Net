namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Input data for File Transfer dialog
    /// </summary>
    public class FileTransferInput
    {
        public byte Type { get; set; }
        public byte MessageSize { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public byte[] FileData { get; set; } = [];
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}