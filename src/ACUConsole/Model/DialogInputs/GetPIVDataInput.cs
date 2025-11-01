namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for get PIV data dialog input
    /// </summary>
    public class GetPIVDataInput
    {
        public byte[] ObjectId { get; set; } = [];
        public byte ElementId { get; set; }
        public byte DataOffset { get; set; }
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}
