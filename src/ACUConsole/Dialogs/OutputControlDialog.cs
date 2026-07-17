using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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
        /// <param name="app">The application instance</param>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>OutputControlInput with user's choices</returns>
        public static OutputControlInput Show(IApplication app, DeviceSetting[] devices, string[] deviceList)
        {
            var result = new OutputControlInput { WasCancelled = true };

            // First, collect output control parameters
            var outputNumberTextField = new TextField { X = 25, Y = 1, Width = 25, Text = "0" };
            var activateOutputCheckBox = new CheckBox { X = 1, Y = 3, Text = "Activate Output", Value = CheckState.UnChecked };

            void NextButtonClicked()
            {
                // Validate output number
                if (!byte.TryParse(outputNumberTextField.Text, out var outputNumber))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid output number entered!", "OK");
                    return;
                }

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show(app, "Output Control", devices, deviceList);

                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.OutputNumber = outputNumber;
                    result.ActivateOutput = activateOutputCheckBox.Value == CheckState.Checked;
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

            var dialog = new Dialog { Title = "Output Control", Width = 60, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Output Number:" }, outputNumberTextField,
                      activateOutputCheckBox);
            dialog.AddButton(cancelButton);
            dialog.AddButton(nextButton);
            outputNumberTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
