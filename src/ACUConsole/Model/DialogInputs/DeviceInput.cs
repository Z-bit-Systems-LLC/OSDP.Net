namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for add and edit device dialog input
    /// </summary>
    public class DeviceInput
    {
        public string Name { get; set; } = string.Empty;
        public byte Address { get; set; }
        public bool UseCrc { get; set; }
        public bool UseSecureChannel { get; set; }
        public byte[] SecureChannelKey { get; set; } = [];
        public bool WasCancelled { get; set; }
    }
}
