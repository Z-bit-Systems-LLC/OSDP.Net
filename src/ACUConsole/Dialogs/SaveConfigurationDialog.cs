using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.Views;

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
        /// <param name="app">The application instance</param>
        /// <param name="currentFilePath">Current config file path for default</param>
        /// <returns>SaveConfigurationInput with user's choices</returns>
        public static SaveConfigurationInput Show(IApplication app, string currentFilePath)
        {
            var result = new SaveConfigurationInput { WasCancelled = true };

            var saveDialog = new SaveDialog
            {
                Title = "Save Configuration",
                Path = currentFilePath ?? "appsettings.config",
                AllowedTypes = { new AllowedType("Configuration", ".config") }
            };
            app.Run(saveDialog);

            if (!saveDialog.Canceled && !string.IsNullOrEmpty(saveDialog.Path))
            {
                result.FilePath = saveDialog.Path;
                result.WasCancelled = false;
            }

            saveDialog.Dispose();
            return result;
        }
    }
}
