using System;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting biometric read parameters and device selection
    /// </summary>
    public static class BiometricReadDialog
    {
        /// <summary>
        /// Shows the biometric read dialog and returns user input
        /// </summary>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>BiometricReadInput with user's choices</returns>
        public static BiometricReadInput Show(DeviceSetting[] devices, string[] deviceList)
        {
            var result = new BiometricReadInput { WasCancelled = true };

            // First, collect biometric read parameters
            var readerNumberTextField = new TextField(25, 1, 25, "0");
            var typeTextField = new TextField(25, 3, 25, "1");
            var formatTextField = new TextField(25, 5, 25, "0");
            var qualityTextField = new TextField(25, 7, 25, "1");

            void NextButtonClicked()
            {
                // Validate reader number
                if (!byte.TryParse(readerNumberTextField.Text.ToString(), out var readerNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reader number entered!", "OK");
                    return;
                }

                // Validate type
                if (!byte.TryParse(typeTextField.Text.ToString(), out var type))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid type entered!", "OK");
                    return;
                }

                // Validate format
                if (!byte.TryParse(formatTextField.Text.ToString(), out var format))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid format entered!", "OK");
                    return;
                }

                // Validate quality
                if (!byte.TryParse(qualityTextField.Text.ToString(), out var quality))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid quality entered!", "OK");
                    return;
                }

                Application.RequestStop();

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show("Biometric Read", devices, deviceList);
                
                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.ReaderNumber = readerNumber;
                    result.Type = type;
                    result.Format = format;
                    result.Quality = quality;
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

            var dialog = new Dialog("Biometric Read", 60, 13, cancelButton, nextButton);
            dialog.Add(new Label(1, 1, "Reader Number:"), readerNumberTextField,
                      new Label(1, 3, "Type:"), typeTextField,
                      new Label(1, 5, "Format:"), formatTextField,
                      new Label(1, 7, "Quality:"), qualityTextField);
            readerNumberTextField.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}