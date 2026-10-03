using System;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>BiometricMatchInput with user's choices</returns>
        public static BiometricMatchInput Show(IApplication app, DeviceSetting[] devices, string[] deviceList)
        {
            var result = new BiometricMatchInput { WasCancelled = true };

            // First, collect biometric match parameters
            var readerNumberTextField = new TextField { X = 25, Y = 1, Width = 25, Text = "0" };
            var typeTextField = new TextField { X = 25, Y = 3, Width = 25, Text = "1" };
            var formatTextField = new TextField { X = 25, Y = 5, Width = 25, Text = "0" };
            var qualityThresholdTextField = new TextField { X = 25, Y = 7, Width = 25, Text = "1" };
            var templateDataTextField = new TextField { X = 25, Y = 9, Width = 40, Text = "" };

            void NextButtonClicked()
            {
                if (!byte.TryParse(readerNumberTextField.Text, out var readerNumber))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid reader number entered!", "OK");
                    return;
                }

                if (!byte.TryParse(typeTextField.Text, out var type))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid type entered!", "OK");
                    return;
                }

                if (!byte.TryParse(formatTextField.Text, out var format))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid format entered!", "OK");
                    return;
                }

                if (!byte.TryParse(qualityThresholdTextField.Text, out var qualityThreshold))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid quality threshold entered!", "OK");
                    return;
                }

                byte[] templateData;
                try
                {
                    var templateDataStr = templateDataTextField.Text;
                    if (string.IsNullOrWhiteSpace(templateDataStr))
                    {
                        MessageBox.ErrorQuery(app, "Error", "Please enter template data!", "OK");
                        return;
                    }
                    templateData = Convert.FromHexString(templateDataStr);
                }
                catch
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid template data hex format!", "OK");
                    return;
                }

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show(app, "Biometric Match", devices, deviceList);

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

                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                app.RequestStop();
            }

            var sendButton = new Button { Text = "Next", IsDefault = true };
            sendButton.Accepting += (_, e) => { NextButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = "Biometric Match", Width = 70, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Reader Number:" }, readerNumberTextField,
                      new Label { X = 1, Y = 3, Text = "Type:" }, typeTextField,
                      new Label { X = 1, Y = 5, Text = "Format:" }, formatTextField,
                      new Label { X = 1, Y = 7, Text = "Quality Threshold:" }, qualityThresholdTextField,
                      new Label { X = 1, Y = 9, Text = "Template Data (hex):" }, templateDataTextField,
                      new Label { X = 1, Y = 11, Text = "Example: '010203040506070809'" });
            dialog.AddButton(cancelButton);
            dialog.AddButton(sendButton);
            readerNumberTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
