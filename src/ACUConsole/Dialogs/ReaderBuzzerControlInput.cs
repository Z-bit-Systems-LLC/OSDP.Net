namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Data transfer object for reader buzzer control dialog input
    /// </summary>
    public class ReaderBuzzerControlInput
    {
        public byte ReaderNumber { get; set; }
        public byte RepeatTimes { get; set; }
        public byte DeviceAddress { get; set; }
        public bool WasCancelled { get; set; }
    }
}