using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>BiometricReadInput with user's choices</returns>
        public static BiometricReadInput Show(IApplication app, DeviceSetting[] devices, string[] deviceList)
        {
            var result = new BiometricReadInput { WasCancelled = true };

            // First, collect biometric read parameters
            var readerNumberTextField = new TextField { X = 25, Y = 1, Width = 25, Text = "0" };
            var typeTextField = new TextField { X = 25, Y = 3, Width = 25, Text = "1" };
            var formatTextField = new TextField { X = 25, Y = 5, Width = 25, Text = "0" };
            var qualityTextField = new TextField { X = 25, Y = 7, Width = 25, Text = "1" };

            void NextButtonClicked()
            {
                // Validate reader number
                if (!byte.TryParse(readerNumberTextField.Text, out var readerNumber))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid reader number entered!", "OK");
                    return;
                }

                // Validate type
                if (!byte.TryParse(typeTextField.Text, out var type))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid type entered!", "OK");
                    return;
                }

                // Validate format
                if (!byte.TryParse(formatTextField.Text, out var format))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid format entered!", "OK");
                    return;
                }

                // Validate quality
                if (!byte.TryParse(qualityTextField.Text, out var quality))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid quality entered!", "OK");
                    return;
                }

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show(app, "Biometric Read", devices, deviceList);

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

            var dialog = new Dialog { Title = "Biometric Read", Width = 60, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Reader Number:" }, readerNumberTextField,
                      new Label { X = 1, Y = 3, Text = "Type:" }, typeTextField,
                      new Label { X = 1, Y = 5, Text = "Format:" }, formatTextField,
                      new Label { X = 1, Y = 7, Text = "Quality:" }, qualityTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(nextButton);
            readerNumberTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
