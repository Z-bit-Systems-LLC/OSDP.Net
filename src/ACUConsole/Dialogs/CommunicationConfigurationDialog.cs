using System;
using System.Collections.ObjectModel;
using System.Linq;
using ACUConsole.Configuration;
using ACUConsole.Extensions;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <param name="currentBaudRate">Current baud rate for default value</param>
        /// <returns>CommunicationConfigurationInput with user's choices</returns>
        public static CommunicationConfigurationInput Show(IApplication app, DeviceSetting[] devices, string[] deviceList, int currentBaudRate)
        {
            var result = new CommunicationConfigurationInput { WasCancelled = true };

            // Calculate suggested new address (highest existing + 1)
            var suggestedAddress = ((devices.MaxBy(device => device.Address)?.Address ?? 0) + 1).ToString();

            // First, collect communication configuration parameters
            var newAddressTextField = new TextField { X = 25, Y = 1, Width = 25, Text = suggestedAddress };
            var newBaudRateComboBox = CreateBaudRateComboBox(25, 3, currentBaudRate)
                .ConfigureForOptimalUX();

            void NextButtonClicked()
            {
                // Validate new address
                if (!byte.TryParse(newAddressTextField.Text, out var newAddress) || newAddress > 127)
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid address entered (0-127)!", "OK");
                    return;
                }

                // Validate new baud rate
                if (!int.TryParse(newBaudRateComboBox.Text, out var newBaudRate))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid baud rate selected!", "OK");
                    return;
                }

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show(app, "Communication Configuration", devices, deviceList);

                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.NewAddress = newAddress;
                    result.NewBaudRate = newBaudRate;
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

            var dialog = new Dialog { Title = "Communication Configuration", Width = 60, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "New Address:" }, newAddressTextField,
                      new Label { X = 1, Y = 3, Text = "New Baud Rate:" }, newBaudRateComboBox);
            dialog.AddButton(cancelButton);
            dialog.AddButton(nextButton);
            newAddressTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }

        private static DropDownList CreateBaudRateComboBox(int x, int y, int currentBaudRate)
        {
            // IMPORTANT: Width must be at least ComboBoxExtensions.MinimumRecommendedWidth (30)
            // for dropdown list to display correctly. See ComboBoxExtensions documentation.
            var baudRateComboBox = new DropDownList
            {
                X = x,
                Y = y,
                Width = 30,
                Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(Constants.StandardBaudRates))
            };

            // Select default baud rate
            var currentBaudRateString = currentBaudRate.ToString();
            var index = Array.FindIndex(Constants.StandardBaudRates, rate =>
                string.Equals(rate, currentBaudRateString));
            baudRateComboBox.Text = Constants.StandardBaudRates[Math.Max(index, 0)];

            return baudRateComboBox;
        }
    }
}
