namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for manufacturer specific command dialog input
    /// </summary>
    public class ManufacturerSpecificInput
    {
        public byte[] VendorCode { get; set; } = [];
        public byte[] Data { get; set; } = [];
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}