using System;
using System.IO.Ports;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting device discovery parameters
    /// </summary>
    public static class DiscoverDeviceDialog
    {
        /// <summary>
        /// Shows the discover device dialog and returns user input
        /// </summary>
        /// <param name="defaultPortName">Default port name to select</param>
        /// <returns>DiscoverDeviceInput with user's choices</returns>
        public static DiscoverDeviceInput Show(string defaultPortName)
        {
            var result = new DiscoverDeviceInput { WasCancelled = true };

            var portNames = SerialPort.GetPortNames();
            var portNameComboBox = new ComboBox(new Rect(15, 1, 35, 5), portNames);
            
            // Select default port name
            if (portNames.Length > 0)
            {
                portNameComboBox.SelectedItem = Math.Max(
                    Array.FindIndex(portNames, port => 
                        string.Equals(port, defaultPortName)), 0);
            }
            var pingTimeoutTextField = new TextField(25, 3, 25, "1000");
            var reconnectDelayTextField = new TextField(25, 5, 25, "0");

            void DiscoverButtonClicked()
            {
                if (string.IsNullOrEmpty(portNameComboBox.Text.ToString()))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "No port name entered!", "OK");
                    return;
                }

                if (!int.TryParse(pingTimeoutTextField.Text.ToString(), out var pingTimeout))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reply timeout entered!", "OK");
                    return;
                }

                if (!int.TryParse(reconnectDelayTextField.Text.ToString(), out var reconnectDelay))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reconnect delay entered!", "OK");
                    return;
                }

                // All validation passed - collect the data
                result.PortName = portNameComboBox.Text.ToString();
                result.PingTimeout = pingTimeout;
                result.ReconnectDelay = reconnectDelay;
                result.WasCancelled = false;
                Application.RequestStop();
            }

            var discoverButton = new Button("Discover", true);
            discoverButton.Clicked += DiscoverButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Discover Device", 60, 11, cancelButton, discoverButton);
            dialog.Add(new Label(1, 1, "Port:"), portNameComboBox,
                      new Label(1, 3, "Ping Timeout(ms):"), pingTimeoutTextField,
                      new Label(1, 5, "Reconnect Delay(ms):"), reconnectDelayTextField);
            pingTimeoutTextField.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}