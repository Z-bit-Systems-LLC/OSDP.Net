using System;
using System.IO.Ports;
using ACUConsole.Configuration;
using ACUConsole.Extensions;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting serial connection parameters
    /// </summary>
    public static class SerialConnectionDialog
    {

        /// <summary>
        /// Shows the serial connection dialog and returns user input
        /// </summary>
        /// <param name="currentSettings">Current serial connection settings for defaults</param>
        /// <returns>SerialConnectionInput with user's choices</returns>
        public static SerialConnectionInput Show(SerialConnectionSettings currentSettings)
        {
            var result = new SerialConnectionInput { WasCancelled = true };

            var portNameComboBox = CreatePortNameComboBox(20, 1, currentSettings.PortName)
                .ConfigureForOptimalUX();
            var baudRateComboBox = CreateBaudRateComboBox(20, 3, currentSettings.BaudRate)
                .ConfigureForOptimalUX();
            var replyTimeoutTextField = new TextField(20, 5, 30, currentSettings.ReplyTimeout.ToString());

            void StartConnectionButtonClicked()
            {
                // Validate port name
                if (string.IsNullOrEmpty(portNameComboBox.Text.ToString()))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "No port name entered!", "OK");
                    return;
                }

                // Validate baud rate
                if (!int.TryParse(baudRateComboBox.Text.ToString(), out var baudRate))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid baud rate selected!", "OK");
                    return;
                }

                // Validate reply timeout
                if (!int.TryParse(replyTimeoutTextField.Text.ToString(), out var replyTimeout))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reply timeout entered!", "OK");
                    return;
                }

                // All validation passed - collect the data
                result.PortName = portNameComboBox.Text.ToString();
                result.BaudRate = baudRate;
                result.ReplyTimeout = replyTimeout;
                result.WasCancelled = false;
                
                Application.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                Application.RequestStop();
            }

            var startButton = new Button("Start", true);
            startButton.Clicked += StartConnectionButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("Start Serial Connection", 60, 10, cancelButton, startButton);
            dialog.Add(new Label(1, 1, "Port:"), portNameComboBox,
                      new Label(1, 3, "Baud Rate:"), baudRateComboBox,
                      new Label(1, 5, "Reply Timeout(ms):"), replyTimeoutTextField);
            portNameComboBox.SetFocus();

            Application.Run(dialog);
            
            return result;
        }

        private static ComboBox CreatePortNameComboBox(int x, int y, string currentPortName)
        {
            var portNames = SerialPort.GetPortNames();
            // IMPORTANT: Width must be at least ComboBoxExtensions.MinimumRecommendedWidth (30)
            // for dropdown list to display correctly. See ComboBoxExtensions documentation.
            var portNameComboBox = new ComboBox(new Rect(x, y, 30, 5), portNames);

            // Select default port name
            if (portNames.Length > 0)
            {
                portNameComboBox.SelectedItem = Math.Max(
                    Array.FindIndex(portNames, port =>
                        string.Equals(port, currentPortName)), 0);
            }

            return portNameComboBox;
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