namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for biometric read dialog input
    /// </summary>
    public class BiometricReadInput
    {
        public byte ReaderNumber { get; set; }
        public byte Type { get; set; }
        public byte Format { get; set; }
        public byte Quality { get; set; }
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}