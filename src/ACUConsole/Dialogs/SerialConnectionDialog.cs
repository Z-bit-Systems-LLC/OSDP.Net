using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using ACUConsole.Configuration;
using ACUConsole.Extensions;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="currentSettings">Current serial connection settings for defaults</param>
        /// <returns>SerialConnectionInput with user's choices</returns>
        public static SerialConnectionInput Show(IApplication app, SerialConnectionSettings currentSettings)
        {
            var result = new SerialConnectionInput { WasCancelled = true };

            var portNameComboBox = CreatePortNameComboBox(20, 1, currentSettings.PortName)
                .ConfigureForOptimalUX();
            var baudRateComboBox = CreateBaudRateComboBox(20, 3, currentSettings.BaudRate)
                .ConfigureForOptimalUX();
            var replyTimeoutTextField = new TextField { X = 20, Y = 5, Width = 30, Text = currentSettings.ReplyTimeout.ToString() };

            void StartConnectionButtonClicked()
            {
                // Validate port name
                if (string.IsNullOrEmpty(portNameComboBox.Text))
                {
                    MessageBox.ErrorQuery(app, "Error", "No port name entered!", "OK");
                    return;
                }

                // Validate baud rate
                if (!int.TryParse(baudRateComboBox.Text, out var baudRate))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid baud rate selected!", "OK");
                    return;
                }

                // Validate reply timeout
                if (!int.TryParse(replyTimeoutTextField.Text, out var replyTimeout))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid reply timeout entered!", "OK");
                    return;
                }

                // All validation passed - collect the data
                result.PortName = portNameComboBox.Text;
                result.BaudRate = baudRate;
                result.ReplyTimeout = replyTimeout;
                result.WasCancelled = false;

                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                app.RequestStop();
            }

            var startButton = new Button { Text = "Start", IsDefault = true };
            startButton.Accepting += (_, e) => { StartConnectionButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = "Start Serial Connection", Width = 60, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Port:" }, portNameComboBox,
                      new Label { X = 1, Y = 3, Text = "Baud Rate:" }, baudRateComboBox,
                      new Label { X = 1, Y = 5, Text = "Reply Timeout(ms):" }, replyTimeoutTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(startButton);
            portNameComboBox.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }

        private static DropDownList CreatePortNameComboBox(int x, int y, string currentPortName)
        {
            var portNames = SerialPort.GetPortNames();
            // IMPORTANT: Width must be at least ComboBoxExtensions.MinimumRecommendedWidth (30)
            // for dropdown list to display correctly. See ComboBoxExtensions documentation.
            var portNameComboBox = new DropDownList
            {
                X = x,
                Y = y,
                Width = 30,
                Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(portNames))
            };

            // Select default port name
            if (portNames.Length > 0)
            {
                var index = Math.Max(
                    Array.FindIndex(portNames, port =>
                        string.Equals(port, currentPortName)), 0);
                portNameComboBox.Text = portNames[index];
            }

            return portNameComboBox;
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
