namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for save configuration dialog input
    /// </summary>
    public class SaveConfigurationInput
    {
        public string FilePath { get; set; } = string.Empty;
        public bool WasCancelled { get; set; }
    }
}