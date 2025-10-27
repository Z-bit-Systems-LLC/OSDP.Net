using System;
using System.IO.Ports;
using PDConsole.Configuration;
using PDConsole.Extensions;
using PDConsole.Model.DialogInputs;
using Terminal.Gui;

namespace PDConsole.Dialogs
{
    /// <summary>
    /// Dialog for configuring serial connection settings
    /// </summary>
    public static class SerialConnectionDialog
    {
        private static readonly string[] StandardBaudRates =
        [
            "9600",
            "19200",
            "38400",
            "57600",
            "115200",
            "230400"
        ];

        /// <summary>
        /// Shows the serial connection configuration dialog and returns user input
        /// </summary>
        /// <param name="currentSettings">Current connection settings for defaults</param>
        /// <returns>SerialConnectionInput with user's choices</returns>
        public static SerialConnectionInput Show(ConnectionSettings currentSettings)
        {
            var result = new SerialConnectionInput { WasCancelled = true };

            var portNameComboBox = CreatePortNameComboBox(15, 1, currentSettings.SerialPortName)
                .ConfigureForOptimalUX();
            var baudRateComboBox = CreateBaudRateComboBox(15, 3, currentSettings.SerialBaudRate)
                .ConfigureForOptimalUX();

            void ApplyButtonClicked()
            {
                // Validate port name
                if (string.IsNullOrEmpty(portNameComboBox.Text.ToString()))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "No port name selected!", "OK");
                    return;
                }

                // Validate baud rate
                if (!int.TryParse(baudRateComboBox.Text.ToString(), out var baudRate))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid baud rate selected!", "OK");
                    return;
                }

                // All validation passed - collect the data
                result.PortName = portNameComboBox.Text.ToString();
                result.BaudRate = baudRate;
                result.WasCancelled = false;

                Application.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                Application.RequestStop();
            }

            var applyButton = new Button("Start", true);
            applyButton.Clicked += ApplyButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("Serial Connection Settings", 60, 10, cancelButton, applyButton);
            dialog.Add(new Label(1, 1, "Port:"), portNameComboBox,
                      new Label(1, 3, "Baud Rate:"), baudRateComboBox);
            portNameComboBox.SetFocus();

            Application.Run(dialog);

            return result;
        }

        private static ComboBox CreatePortNameComboBox(int x, int y, string currentPortName)
        {
            var portNames = SerialPort.GetPortNames();

            // If no ports are available, show a message
            if (portNames.Length == 0)
            {
                portNames = ["No ports available"];
            }

            var portNameComboBox = new ComboBox(new Rect(x, y, 30, 5), portNames);

            // Select default port name
            if (portNames.Length > 0 && !portNames[0].Equals("No ports available"))
            {
                var index = Array.FindIndex(portNames, port =>
                    string.Equals(port, currentPortName, StringComparison.OrdinalIgnoreCase));
                portNameComboBox.SelectedItem = Math.Max(index, 0);
            }

            return portNameComboBox;
        }

        private static ComboBox CreateBaudRateComboBox(int x, int y, int currentBaudRate)
        {
            var baudRateComboBox = new ComboBox(new Rect(x, y, 30, 5), StandardBaudRates);

            // Select default baud rate
            var currentBaudRateString = currentBaudRate.ToString();
            var index = Array.FindIndex(StandardBaudRates, rate =>
                string.Equals(rate, currentBaudRateString));
            baudRateComboBox.SelectedItem = Math.Max(index, 0);

            return baudRateComboBox;
        }
    }
}