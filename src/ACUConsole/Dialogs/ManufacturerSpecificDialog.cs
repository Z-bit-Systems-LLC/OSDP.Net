using System;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

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
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>ManufacturerSpecificInput with user's choices</returns>
        public static ManufacturerSpecificInput Show(DeviceSetting[] devices, string[] deviceList)
        {
            var result = new ManufacturerSpecificInput { WasCancelled = true };

            // First, collect manufacturer specific parameters
            var vendorCodeTextField = new TextField(25, 1, 25, "");
            var dataTextField = new TextField(25, 3, 40, "");

            void NextButtonClicked()
            {
                // Validate vendor code
                byte[] vendorCode;
                try
                {
                    var vendorCodeStr = vendorCodeTextField.Text.ToString();
                    if (string.IsNullOrWhiteSpace(vendorCodeStr))
                    {
                        MessageBox.ErrorQuery(40, 10, "Error", "Please enter vendor code!", "OK");
                        return;
                    }
                    vendorCode = Convert.FromHexString(vendorCodeStr);
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid vendor code hex format!", "OK");
                    return;
                }

                if (vendorCode.Length != 3)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Vendor code must be exactly 3 bytes!", "OK");
                    return;
                }

                // Validate data
                byte[] data;
                try
                {
                    var dataStr = dataTextField.Text.ToString();
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
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid data hex format!", "OK");
                    return;
                }

                Application.RequestStop();

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show("Manufacturer Specific", devices, deviceList);
                
                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.VendorCode = vendorCode;
                    result.Data = data;
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

            var dialog = new Dialog("Manufacturer Specific Command", 70, 11, cancelButton, nextButton);
            dialog.Add(new Label(1, 1, "Vendor Code (hex):"), vendorCodeTextField,
                      new Label(1, 3, "Data (hex):"), dataTextField);
            vendorCodeTextField.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}