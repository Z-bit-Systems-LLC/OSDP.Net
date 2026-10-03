using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for setting ACU receive size and device selection
    /// </summary>
    public static class ACUReceiveSizeDialog
    {
        /// <summary>
        /// Shows the ACU receive size dialog and returns user input
        /// </summary>
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>ACUReceiveSizeInput with user's choices</returns>
        public static ACUReceiveSizeInput Show(IApplication app, DeviceSetting[] devices, string[] deviceList)
        {
            var result = new ACUReceiveSizeInput { WasCancelled = true };

            var maximumReceiveSizeTextField = new TextField { X = 31, Y = 1, Width = 15, Text = "128" };

            void NextButtonClicked()
            {
                // Validate maximum receive size
                if (!byte.TryParse(maximumReceiveSizeTextField.Text, out var maximumReceiveSize))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid maximum receive size entered!", "OK");
                    return;
                }

                if (maximumReceiveSize == 0)
                {
                    MessageBox.ErrorQuery(app, "Error", "Maximum receive size must be greater than 0!", "OK");
                    return;
                }

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show(app, "ACU Receive Size", devices, deviceList);

                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.MaximumReceiveSize = maximumReceiveSize;
                    result.DeviceAddress = deviceSelection.SelectedDeviceAddress;
                    result.WasCancelled = false;
                }

                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                app.RequestStop();
            }

            var nextButton = new Button { Text = "Next", IsDefault = true };
            nextButton.Accepting += (_, e) => { NextButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = "ACU Receive Size", Width = 60, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Max Receive Size (bytes):" }, maximumReceiveSizeTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(nextButton);
            maximumReceiveSizeTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
