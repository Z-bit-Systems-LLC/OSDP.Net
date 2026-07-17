using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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
        /// <param name="app">The application instance</param>
        /// <param name="currentPollingInterval">Current polling interval value</param>
        /// <param name="currentIsTracing">Current tracing setting</param>
        /// <returns>ConnectionSettingsInput with user's choices</returns>
        public static ConnectionSettingsInput Show(IApplication app, int currentPollingInterval, bool currentIsTracing)
        {
            var result = new ConnectionSettingsInput { WasCancelled = true };

            var pollingIntervalTextField = new TextField { X = 25, Y = 4, Width = 25, Text = currentPollingInterval.ToString() };
            var tracingCheckBox = new CheckBox
            {
                X = 1,
                Y = 6,
                Text = "Write packet data to file",
                Value = currentIsTracing ? CheckState.Checked : CheckState.UnChecked
            };

            void UpdateConnectionSettingsButtonClicked()
            {
                // Validate polling interval
                if (!int.TryParse(pollingIntervalTextField.Text, out var pollingInterval))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid polling interval entered!", "OK");
                    return;
                }

                // All validation passed - collect the data
                result.PollingInterval = pollingInterval;
                result.IsTracing = tracingCheckBox.Value == CheckState.Checked;
                result.WasCancelled = false;

                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                app.RequestStop();
            }

            var updateButton = new Button { Text = "Update", IsDefault = true };
            updateButton.Accepting += (_, e) => { UpdateConnectionSettingsButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = "Update Connection Settings", Width = 80, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Width = 75, Height = 2, Text = "Connection will need to be restarted for setting to take effect." },
                      new Label { X = 1, Y = 4, Text = "Polling Interval(ms):" }, pollingIntervalTextField,
                      tracingCheckBox);
            dialog.AddButton(cancelButton);
            dialog.AddButton(updateButton);

            pollingIntervalTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
