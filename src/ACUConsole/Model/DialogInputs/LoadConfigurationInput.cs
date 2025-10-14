namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for load configuration dialog input
    /// </summary>
    public class LoadConfigurationInput
    {
        public string FilePath { get; set; } = string.Empty;
        public bool WasCancelled { get; set; }
    }
}
