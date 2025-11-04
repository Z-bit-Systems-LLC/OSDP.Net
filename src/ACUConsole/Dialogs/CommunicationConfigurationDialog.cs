using System;
using System.Linq;
using ACUConsole.Configuration;
using ACUConsole.Extensions;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting communication configuration parameters and device selection
    /// </summary>
    public static class CommunicationConfigurationDialog
    {
        /// <summary>
        /// Shows the communication configuration dialog and returns user input
        /// </summary>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <param name="currentBaudRate">Current baud rate for default value</param>
        /// <returns>CommunicationConfigurationInput with user's choices</returns>
        public static CommunicationConfigurationInput Show(DeviceSetting[] devices, string[] deviceList, int currentBaudRate)
        {
            var result = new CommunicationConfigurationInput { WasCancelled = true };

            // Calculate suggested new address (highest existing + 1)
            var suggestedAddress = ((devices.MaxBy(device => device.Address)?.Address ?? 0) + 1).ToString();

            // First, collect communication configuration parameters
            var newAddressTextField = new TextField(25, 1, 25, suggestedAddress);
            var newBaudRateComboBox = CreateBaudRateComboBox(25, 3, currentBaudRate)
                .ConfigureForOptimalUX();

            void NextButtonClicked()
            {
                // Validate new address
                if (!byte.TryParse(newAddressTextField.Text.ToString(), out var newAddress) || newAddress > 127)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid address entered (0-127)!", "OK");
                    return;
                }

                // Validate new baud rate
                if (!int.TryParse(newBaudRateComboBox.Text.ToString(), out var newBaudRate))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid baud rate selected!", "OK");
                    return;
                }

                Application.RequestStop();

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show("Communication Configuration", devices, deviceList);
                
                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.NewAddress = newAddress;
                    result.NewBaudRate = newBaudRate;
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

            var dialog = new Dialog("Communication Configuration", 60, 11, cancelButton, nextButton);
            dialog.Add(new Label(1, 1, "New Address:"), newAddressTextField,
                      new Label(1, 3, "New Baud Rate:"), newBaudRateComboBox);
            newAddressTextField.SetFocus();

            Application.Run(dialog);

            return result;
        }

        private static ComboBox CreateBaudRateComboBox(int x, int y, int currentBaudRate)
        {
            // IMPORTANT: Width must be at least ComboBoxExtensions.MinimumRecommendedWidth (30)
            // for dropdown list to display correctly. See ComboBoxExtensions documentation.
            var baudRateComboBox = new ComboBox(new Rect(x, y, 30, 5), Constants.StandardBaudRates);

            // Select default baud rate
            var currentBaudRateString = currentBaudRate.ToString();
            var index = Array.FindIndex(Constants.StandardBaudRates, rate =>
                string.Equals(rate, currentBaudRateString));
            baudRateComboBox.SelectedItem = Math.Max(index, 0);

            return baudRateComboBox;
        }
    }
}