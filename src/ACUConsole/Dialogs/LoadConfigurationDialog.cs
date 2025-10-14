using System.IO;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

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
        /// <returns>LoadConfigurationInput with user's choices</returns>
        public static LoadConfigurationInput Show()
        {
            var result = new LoadConfigurationInput { WasCancelled = true };

            var openDialog = new OpenDialog("Load Configuration", string.Empty, [".config"]);
            Application.Run(openDialog);

            if (!openDialog.Canceled && !string.IsNullOrEmpty(openDialog.FilePath?.ToString()))
            {
                var filePath = openDialog.FilePath.ToString();

                if (File.Exists(filePath))
                {
                    result.FilePath = filePath;
                    result.WasCancelled = false;
                }
                else
                {
                    MessageBox.ErrorQuery(40, 8, "Error", "Selected file does not exist", "OK");
                }
            }

            return result;
        }
    }
}