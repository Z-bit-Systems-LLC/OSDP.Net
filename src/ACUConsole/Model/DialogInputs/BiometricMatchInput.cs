namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Input data for Biometric Match dialog
    /// </summary>
    public class BiometricMatchInput
    {
        public byte ReaderNumber { get; set; }
        public byte Type { get; set; }
        public byte Format { get; set; }
        public byte QualityThreshold { get; set; }
        public byte[] TemplateData { get; set; } = [];
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}