using PDConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.Views;

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
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="currentFilePath">Current settings file path for default</param>
        /// <returns>SaveSettingsInput with user's choices</returns>
        public static SaveSettingsInput Show(IApplication app, string currentFilePath)
        {
            var result = new SaveSettingsInput { WasCancelled = true };

            var saveDialog = new SaveDialog
            {
                Title = "Save Settings",
                Path = currentFilePath ?? "appsettings.json",
                AllowedTypes = { new AllowedType("JSON", ".json") }
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
