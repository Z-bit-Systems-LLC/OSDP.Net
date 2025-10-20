using System.IO;
using PDConsole.Model.DialogInputs;
using Terminal.Gui;

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
        public static LoadSettingsInput Show()
        {
            var result = new LoadSettingsInput { WasCancelled = true };

            var openDialog = new OpenDialog("Load Settings", string.Empty, [".json"]);
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