using System;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting manufacturer specific command parameters and device selection
    /// </summary>
    public static class ManufacturerSpecificDialog
    {
        /// <summary>
        /// Shows the manufacturer specific command dialog and returns user input
        /// </summary>
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>ManufacturerSpecificInput with user's choices</returns>
        public static ManufacturerSpecificInput Show(IApplication app, DeviceSetting[] devices, string[] deviceList)
        {
            var result = new ManufacturerSpecificInput { WasCancelled = true };

            // First, collect manufacturer specific parameters
            var vendorCodeTextField = new TextField { X = 25, Y = 1, Width = 25, Text = "" };
            var dataTextField = new TextField { X = 25, Y = 3, Width = 40, Text = "" };

            void NextButtonClicked()
            {
                // Validate vendor code
                byte[] vendorCode;
                try
                {
                    var vendorCodeStr = vendorCodeTextField.Text;
                    if (string.IsNullOrWhiteSpace(vendorCodeStr))
                    {
                        MessageBox.ErrorQuery(app, "Error", "Please enter vendor code!", "OK");
                        return;
                    }
                    vendorCode = Convert.FromHexString(vendorCodeStr);
                }
                catch
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid vendor code hex format!", "OK");
                    return;
                }

                if (vendorCode.Length != 3)
                {
                    MessageBox.ErrorQuery(app, "Error", "Vendor code must be exactly 3 bytes!", "OK");
                    return;
                }

                // Validate data
                byte[] data;
                try
                {
                    var dataStr = dataTextField.Text;
                    if (string.IsNullOrWhiteSpace(dataStr))
                    {
                        data = Array.Empty<byte>();
                    }
                    else
                    {
                        data = Convert.FromHexString(dataStr);
                    }
                }
                catch
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid data hex format!", "OK");
                    return;
                }

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show(app, "Manufacturer Specific", devices, deviceList);

                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.VendorCode = vendorCode;
                    result.Data = data;
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

            var dialog = new Dialog { Title = "Manufacturer Specific Command", Width = 70, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Vendor Code (hex):" }, vendorCodeTextField,
                      new Label { X = 1, Y = 3, Text = "Data (hex):" }, dataTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(nextButton);
            vendorCodeTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
