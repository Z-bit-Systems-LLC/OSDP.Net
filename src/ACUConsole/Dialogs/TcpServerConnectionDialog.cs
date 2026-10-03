using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting TCP server connection parameters
    /// </summary>
    public static class TcpServerConnectionDialog
    {
        /// <summary>
        /// Shows the TCP server connection dialog and returns user input
        /// </summary>
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="currentSettings">Current TCP server connection settings for defaults</param>
        /// <returns>TcpServerConnectionInput with user's choices</returns>
        public static TcpServerConnectionInput Show(IApplication app, TcpServerConnectionSettings currentSettings)
        {
            var result = new TcpServerConnectionInput { WasCancelled = true };

            var portNumberTextField = new TextField { X = 25, Y = 1, Width = 25, Text = currentSettings.PortNumber.ToString() };
            var baudRateTextField = new TextField { X = 25, Y = 3, Width = 25, Text = currentSettings.BaudRate.ToString() };
            var replyTimeoutTextField = new TextField { X = 25, Y = 5, Width = 25, Text = currentSettings.ReplyTimeout.ToString() };

            void StartConnectionButtonClicked()
            {
                // Validate port number
                if (!int.TryParse(portNumberTextField.Text, out var portNumber))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid port number entered!", "OK");
                    return;
                }

                // Validate baud rate
                if (!int.TryParse(baudRateTextField.Text, out var baudRate))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid baud rate entered!", "OK");
                    return;
                }

                // Validate reply timeout
                if (!int.TryParse(replyTimeoutTextField.Text, out var replyTimeout))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid reply timeout entered!", "OK");
                    return;
                }

                // All validation passed - collect the data
                result.PortNumber = portNumber;
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

            var dialog = new Dialog { Title = "Start TCP Server Connection", Width = 60, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Port Number:" }, portNumberTextField,
                      new Label { X = 1, Y = 3, Text = "Baud Rate:" }, baudRateTextField,
                      new Label { X = 1, Y = 5, Text = "Reply Timeout(ms):" }, replyTimeoutTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(startButton);
            portNumberTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
