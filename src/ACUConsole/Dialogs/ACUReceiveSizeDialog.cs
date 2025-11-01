using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

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
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>ACUReceiveSizeInput with user's choices</returns>
        public static ACUReceiveSizeInput Show(DeviceSetting[] devices, string[] deviceList)
        {
            var result = new ACUReceiveSizeInput { WasCancelled = true };

            var maximumReceiveSizeTextField = new TextField(31, 1, 15, "128");

            void NextButtonClicked()
            {
                // Validate maximum receive size
                if (!byte.TryParse(maximumReceiveSizeTextField.Text.ToString(), out var maximumReceiveSize))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid maximum receive size entered!", "OK");
                    return;
                }

                if (maximumReceiveSize == 0)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Maximum receive size must be greater than 0!", "OK");
                    return;
                }

                Application.RequestStop();

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show("ACU Receive Size", devices, deviceList);

                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.MaximumReceiveSize = maximumReceiveSize;
                    result.DeviceAddress = deviceSelection.SelectedDeviceAddress;
                    result.WasCancelled = false;
                }
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                Application.RequestStop();
            }

            var nextButton = new Button("Next", true);
            nextButton.Clicked += NextButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("ACU Receive Size", 60, 10, cancelButton, nextButton);
            dialog.Add(new Label(1, 1, "Max Receive Size (bytes):"), maximumReceiveSizeTextField);
            maximumReceiveSizeTextField.SetFocus();

            Application.Run(dialog);

            return result;
        }
    }
}
