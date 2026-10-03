using System.IO;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for loading configuration from file
    /// </summary>
    public static class LoadConfigurationDialog
    {
        /// <summary>
        /// Shows the load configuration dialog and returns user input
        /// </summary>
        /// <param name="app">The application instance</param>
        /// <returns>LoadConfigurationInput with user's choices</returns>
        public static LoadConfigurationInput Show(IApplication app)
        {
            var result = new LoadConfigurationInput { WasCancelled = true };

            var openDialog = new OpenDialog
            {
                Title = "Load Configuration",
                AllowedTypes = { new AllowedType("Configuration", ".config") }
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
