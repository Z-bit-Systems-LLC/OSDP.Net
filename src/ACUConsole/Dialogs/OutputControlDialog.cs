using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting output control parameters and device selection
    /// </summary>
    public static class OutputControlDialog
    {
        /// <summary>
        /// Shows the output control dialog and returns user input
        /// </summary>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>OutputControlInput with user's choices</returns>
        public static OutputControlInput Show(DeviceSetting[] devices, string[] deviceList)
        {
            var result = new OutputControlInput { WasCancelled = true };

            // First, collect output control parameters
            var outputNumberTextField = new TextField(25, 1, 25, "0");
            var activateOutputCheckBox = new CheckBox(1, 3, "Activate Output", false);

            void NextButtonClicked()
            {
                // Validate output number
                if (!byte.TryParse(outputNumberTextField.Text.ToString(), out var outputNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid output number entered!", "OK");
                    return;
                }

                Application.RequestStop();

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show("Output Control", devices, deviceList);
                
                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.OutputNumber = outputNumber;
                    result.ActivateOutput = activateOutputCheckBox.Checked;
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

            var dialog = new Dialog("Output Control", 60, 10, cancelButton, nextButton);
            dialog.Add(new Label(1, 1, "Output Number:"), outputNumberTextField,
                      activateOutputCheckBox);
            outputNumberTextField.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}