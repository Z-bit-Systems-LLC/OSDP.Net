using System.IO;
using PDConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace PDConsole.Dialogs
{
    /// <summary>
    /// Dialog for loading settings from file
    /// </summary>
    public static class LoadSettingsDialog
    {
        /// <summary>
        /// Shows the load settings dialog and returns user input
        /// </summary>
        /// <returns>LoadSettingsInput with user's choices</returns>
        public static LoadSettingsInput Show(IApplication app)
        {
            var result = new LoadSettingsInput { WasCancelled = true };

            var openDialog = new OpenDialog
            {
                Title = "Load Settings",
                AllowedTypes = { new AllowedType("JSON", ".json") }
            };
            app.Run(openDialog);

            if (!openDialog.Canceled && !string.IsNullOrEmpty(openDialog.Path))
            {
                var filePath = openDialog.Path;

                if (File.Exists(filePath))
                {
                    result.FilePath = filePath;
                    result.WasCancelled = false;
                }
                else
                {
                    MessageBox.ErrorQuery(app, "Error", "Selected file does not exist", "OK");
                }
            }

            openDialog.Dispose();
            return result;
        }
    }
}
