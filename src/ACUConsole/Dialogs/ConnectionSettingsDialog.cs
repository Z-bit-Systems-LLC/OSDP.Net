using System;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for updating connection settings
    /// </summary>
    public static class ConnectionSettingsDialog
    {
        /// <summary>
        /// Shows the connection settings dialog and returns user input
        /// </summary>
        /// <param name="currentPollingInterval">Current polling interval value</param>
        /// <param name="currentIsTracing">Current tracing setting</param>
        /// <returns>ConnectionSettingsInput with user's choices</returns>
        public static ConnectionSettingsInput Show(int currentPollingInterval, bool currentIsTracing)
        {
            var result = new ConnectionSettingsInput { WasCancelled = true };

            var pollingIntervalTextField = new TextField(25, 4, 25, currentPollingInterval.ToString());
            var tracingCheckBox = new CheckBox(1, 6, "Write packet data to file", currentIsTracing);

            void UpdateConnectionSettingsButtonClicked()
            {
                // Validate polling interval
                if (!int.TryParse(pollingIntervalTextField.Text.ToString(), out var pollingInterval))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid polling interval entered!", "OK");
                    return;
                }

                // All validation passed - collect the data
                result.PollingInterval = pollingInterval;
                result.IsTracing = tracingCheckBox.Checked;
                result.WasCancelled = false;
                
                Application.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                Application.RequestStop();
            }

            var updateButton = new Button("Update", true);
            updateButton.Clicked += UpdateConnectionSettingsButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("Update Connection Settings", 60, 12, cancelButton, updateButton);
            dialog.Add(new Label(new Rect(1, 1, 55, 2), "Connection will need to be restarted for setting to take effect."),
                      new Label(1, 4, "Polling Interval(ms):"), pollingIntervalTextField,
                      tracingCheckBox);
            pollingIntervalTextField.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}