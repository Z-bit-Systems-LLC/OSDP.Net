namespace PDConsole.Model.DialogInputs
{
    /// <summary>
    /// Input model for save settings dialog
    /// </summary>
    public class SaveSettingsInput
    {
        /// <summary>
        /// The file path selected by the user
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Whether the dialog was cancelled
        /// </summary>
        public bool WasCancelled { get; set; }
    }
}