using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using OSDP.Net.Connections;
using PDConsole.Configuration;
using PDConsole.Extensions;
using PDConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace PDConsole.Dialogs
{
    /// <summary>
    /// Dialog for configuring serial connection settings
    /// </summary>
    public static class SerialConnectionDialog
    {
        private static readonly string[] StandardBaudRates =
            SerialPortOsdpConnection.StandardBaudRates.Select(r => r.ToString()).ToArray();

        /// <summary>
        /// Shows the serial connection configuration dialog and returns user input
        /// </summary>
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="currentSettings">Current connection settings for defaults</param>
        /// <returns>SerialConnectionInput with user's choices</returns>
        public static SerialConnectionInput Show(IApplication app, ConnectionSettings currentSettings)
        {
            var result = new SerialConnectionInput { WasCancelled = true };

            var portNameComboBox = CreatePortNameComboBox(15, 1, currentSettings.SerialPortName)
                .ConfigureForOptimalUX();
            var baudRateComboBox = CreateBaudRateComboBox(15, 3, currentSettings.SerialBaudRate)
                .ConfigureForOptimalUX();

            void ApplyButtonClicked()
            {
                // Validate port name
                if (string.IsNullOrEmpty(portNameComboBox.Text))
                {
                    MessageBox.ErrorQuery(app, "Error", "No port name selected!", "OK");
                    return;
                }

                // Validate baud rate
                if (!int.TryParse(baudRateComboBox.Text, out var baudRate))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid baud rate selected!", "OK");
                    return;
                }

                // All validation passed - collect the data
                result.PortName = portNameComboBox.Text;
                result.BaudRate = baudRate;
                result.WasCancelled = false;

                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                app.RequestStop();
            }

            var applyButton = new Button { Text = "Start", IsDefault = true };
            applyButton.Accepting += (_, e) => { ApplyButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog
            {
                Title = "Serial Connection Settings",
                Width = 60,
                Height = Dim.Auto()
            };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Port:" }, portNameComboBox,
                      new Label { X = 1, Y = 3, Text = "Baud Rate:" }, baudRateComboBox);
            dialog.AddButton(cancelButton);
            dialog.AddButton(applyButton);
            portNameComboBox.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }

        private static DropDownList CreatePortNameComboBox(int x, int y, string currentPortName)
        {
            var portNames = SerialPort.GetPortNames();

            // If no ports are available, show a message
            if (portNames.Length == 0)
            {
                portNames = ["No ports available"];
            }

            var portNameComboBox = new DropDownList
            {
                X = x,
                Y = y,
                Width = 30,
                Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(portNames))
            };

            // Select default port name
            if (!portNames[0].Equals("No ports available"))
            {
                var index = Array.FindIndex(portNames, port =>
                    string.Equals(port, currentPortName, StringComparison.OrdinalIgnoreCase));
                portNameComboBox.Text = portNames[Math.Max(index, 0)];
            }

            return portNameComboBox;
        }

        private static DropDownList CreateBaudRateComboBox(int x, int y, int currentBaudRate)
        {
            var baudRateComboBox = new DropDownList
            {
                X = x,
                Y = y,
                Width = 30,
                Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(StandardBaudRates))
            };

            // Select default baud rate
            var currentBaudRateString = currentBaudRate.ToString();
            var index = Array.FindIndex(StandardBaudRates, rate =>
                string.Equals(rate, currentBaudRateString));
            baudRateComboBox.Text = StandardBaudRates[Math.Max(index, 0)];

            return baudRateComboBox;
        }
    }
}
