using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for saving configuration to file
    /// </summary>
    public static class SaveConfigurationDialog
    {
        /// <summary>
        /// Shows the save configuration dialog and returns user input
        /// </summary>
        /// <param name="currentFilePath">Current config file path for default</param>
        /// <returns>SaveConfigurationInput with user's choices</returns>
        public static SaveConfigurationInput Show(string currentFilePath)
        {
            var result = new SaveConfigurationInput { WasCancelled = true };

            var saveDialog = new SaveDialog("Save Configuration", string.Empty, [".config"])
            {
                FilePath = currentFilePath ?? "appsettings.config"
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