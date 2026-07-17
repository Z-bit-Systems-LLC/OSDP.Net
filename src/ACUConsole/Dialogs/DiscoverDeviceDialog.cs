using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using ACUConsole.Extensions;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="defaultPortName">Default port name to select</param>
        /// <returns>DiscoverDeviceInput with user's choices</returns>
        public static DiscoverDeviceInput Show(IApplication app, string defaultPortName)
        {
            var result = new DiscoverDeviceInput { WasCancelled = true };

            var portNames = SerialPort.GetPortNames();
            // IMPORTANT: Width must be at least ComboBoxExtensions.MinimumRecommendedWidth (30)
            // for dropdown list to display correctly. See ComboBoxExtensions documentation.
            var portNameComboBox = new DropDownList
            {
                X = 15,
                Y = 1,
                Width = 35,
                Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(portNames))
            };

            // Select default port name
            if (portNames.Length > 0)
            {
                var index = Math.Max(
                    Array.FindIndex(portNames, port =>
                        string.Equals(port, defaultPortName)), 0);
                portNameComboBox.Text = portNames[index];
            }

            portNameComboBox.ConfigureForOptimalUX();
            var pingTimeoutTextField = new TextField { X = 25, Y = 3, Width = 25, Text = "1000" };
            var reconnectDelayTextField = new TextField { X = 25, Y = 5, Width = 25, Text = "0" };

            void DiscoverButtonClicked()
            {
                if (string.IsNullOrEmpty(portNameComboBox.Text))
                {
                    MessageBox.ErrorQuery(app, "Error", "No port name entered!", "OK");
                    return;
                }

                if (!int.TryParse(pingTimeoutTextField.Text, out var pingTimeout))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid reply timeout entered!", "OK");
                    return;
                }

                if (!int.TryParse(reconnectDelayTextField.Text, out var reconnectDelay))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid reconnect delay entered!", "OK");
                    return;
                }

                // All validation passed - collect the data
                result.PortName = portNameComboBox.Text;
                result.PingTimeout = pingTimeout;
                result.ReconnectDelay = reconnectDelay;
                result.WasCancelled = false;
                app.RequestStop();
            }

            var discoverButton = new Button { Text = "Discover", IsDefault = true };
            discoverButton.Accepting += (_, e) => { DiscoverButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { app.RequestStop(); e.Handled = true; };

            var dialog = new Dialog { Title = "Discover Device", Width = 60, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Port:" }, portNameComboBox,
                      new Label { X = 1, Y = 3, Text = "Ping Timeout(ms):" }, pingTimeoutTextField,
                      new Label { X = 1, Y = 5, Text = "Reconnect Delay(ms):" }, reconnectDelayTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(discoverButton);
            pingTimeoutTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
