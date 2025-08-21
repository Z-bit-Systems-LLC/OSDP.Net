using System;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting biometric match parameters and device selection
    /// </summary>
    public static class BiometricMatchDialog
    {
        /// <summary>
        /// Shows the biometric match dialog and returns user input
        /// </summary>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>BiometricMatchInput with user's choices</returns>
        public static BiometricMatchInput Show(DeviceSetting[] devices, string[] deviceList)
        {
            var result = new BiometricMatchInput { WasCancelled = true };

            // First, collect biometric match parameters
            var readerNumberTextField = new TextField(25, 1, 25, "0");
            var typeTextField = new TextField(25, 3, 25, "1");
            var formatTextField = new TextField(25, 5, 25, "0");
            var qualityThresholdTextField = new TextField(25, 7, 25, "1");
            var templateDataTextField = new TextField(25, 9, 40, "");

            void NextButtonClicked()
            {
                if (!byte.TryParse(readerNumberTextField.Text.ToString(), out var readerNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reader number entered!", "OK");
                    return;
                }

                if (!byte.TryParse(typeTextField.Text.ToString(), out var type))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid type entered!", "OK");
                    return;
                }

                if (!byte.TryParse(formatTextField.Text.ToString(), out var format))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid format entered!", "OK");
                    return;
                }

                if (!byte.TryParse(qualityThresholdTextField.Text.ToString(), out var qualityThreshold))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid quality threshold entered!", "OK");
                    return;
                }

                byte[] templateData;
                try
                {
                    var templateDataStr = templateDataTextField.Text.ToString();
                    if (string.IsNullOrWhiteSpace(templateDataStr))
                    {
                        MessageBox.ErrorQuery(40, 10, "Error", "Please enter template data!", "OK");
                        return;
                    }
                    templateData = Convert.FromHexString(templateDataStr);
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid template data hex format!", "OK");
                    return;
                }

                Application.RequestStop();

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show("Biometric Match", devices, deviceList);
                
                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.ReaderNumber = readerNumber;
                    result.Type = type;
                    result.Format = format;
                    result.QualityThreshold = qualityThreshold;
                    result.TemplateData = templateData;
                    result.DeviceAddress = deviceSelection.SelectedDeviceAddress;
                    result.WasCancelled = false;
                }
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                Application.RequestStop();
            }

            var sendButton = new Button("Next", true);
            sendButton.Clicked += NextButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("Biometric Match", 70, 17, cancelButton, sendButton);
            dialog.Add(new Label(1, 1, "Reader Number:"), readerNumberTextField,
                      new Label(1, 3, "Type:"), typeTextField,
                      new Label(1, 5, "Format:"), formatTextField,
                      new Label(1, 7, "Quality Threshold:"), qualityThresholdTextField,
                      new Label(1, 9, "Template Data (hex):"), templateDataTextField,
                      new Label(1, 11, "Example: '010203040506070809'"));
            readerNumberTextField.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}