using System;
using System.IO.Ports;
using ACUConsole.Configuration;
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

            var portNameComboBox = CreatePortNameComboBox(15, 1, currentSettings.PortName);
            var baudRateTextField = new TextField(25, 3, 25, currentSettings.BaudRate.ToString());
            var replyTimeoutTextField = new TextField(25, 5, 25, currentSettings.ReplyTimeout.ToString());

            void StartConnectionButtonClicked()
            {
                // Validate port name
                if (string.IsNullOrEmpty(portNameComboBox.Text.ToString()))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "No port name entered!", "OK");
                    return;
                }
                
                // Validate baud rate
                if (!int.TryParse(baudRateTextField.Text.ToString(), out var baudRate))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid baud rate entered!", "OK");
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

            var dialog = new Dialog("Start Serial Connection", 70, 12, cancelButton, startButton);
            dialog.Add(new Label(1, 1, "Port:"), portNameComboBox,
                      new Label(1, 3, "Baud Rate:"), baudRateTextField,
                      new Label(1, 5, "Reply Timeout(ms):"), replyTimeoutTextField);
            portNameComboBox.SetFocus();

            Application.Run(dialog);
            
            return result;
        }

        private static ComboBox CreatePortNameComboBox(int x, int y, string currentPortName)
        {
            var portNames = SerialPort.GetPortNames();
            var portNameComboBox = new ComboBox(new Rect(x, y, 35, 5), portNames);

            // Select default port name
            if (portNames.Length > 0)
            {
                portNameComboBox.SelectedItem = Math.Max(
                    Array.FindIndex(portNames, port => 
                        string.Equals(port, currentPortName)), 0);
            }

            return portNameComboBox;
        }
    }
}