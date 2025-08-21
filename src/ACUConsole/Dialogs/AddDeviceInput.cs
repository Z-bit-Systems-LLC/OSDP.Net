namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Data transfer object for add device dialog input
    /// </summary>
    public class AddDeviceInput
    {
        public string Name { get; set; } = string.Empty;
        public byte Address { get; set; }
        public bool UseCrc { get; set; }
        public bool UseSecureChannel { get; set; }
        public byte[] SecureChannelKey { get; set; } = [];
        public bool WasCancelled { get; set; }
        public bool OverwriteExisting { get; set; }
    }
}