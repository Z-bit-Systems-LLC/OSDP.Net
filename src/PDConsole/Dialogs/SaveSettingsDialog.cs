using PDConsole.Model.DialogInputs;
using Terminal.Gui;

namespace PDConsole.Dialogs
{
    /// <summary>
    /// Dialog for saving settings to file
    /// </summary>
    public static class SaveSettingsDialog
    {
        /// <summary>
        /// Shows the save settings dialog and returns user input
        /// </summary>
        /// <param name="currentFilePath">Current settings file path for default</param>
        /// <returns>SaveSettingsInput with user's choices</returns>
        public static SaveSettingsInput Show(string currentFilePath)
        {
            var result = new SaveSettingsInput { WasCancelled = true };

            var saveDialog = new SaveDialog("Save Settings", string.Empty, [".json"])
            {
                FilePath = currentFilePath ?? "appsettings.json"
            };
            Application.Run(saveDialog);

            if (!saveDialog.Canceled && !string.IsNullOrEmpty(saveDialog.FilePath?.ToString()))
            {
                result.FilePath = saveDialog.FilePath.ToString();
                result.WasCancelled = false;
            }

            return result;
        }
    }
}