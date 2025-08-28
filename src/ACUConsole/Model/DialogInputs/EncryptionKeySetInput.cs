namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Input data for Encryption Key Set dialog
    /// </summary>
    public class EncryptionKeySetInput
    {
        public byte[] EncryptionKey { get; set; } = [];
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}