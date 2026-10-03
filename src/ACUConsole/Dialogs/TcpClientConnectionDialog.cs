using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting TCP client connection parameters
    /// </summary>
    public static class TcpClientConnectionDialog
    {
        /// <summary>
        /// Shows the TCP client connection dialog and returns user input
        /// </summary>
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="currentSettings">Current TCP client connection settings for defaults</param>
        /// <returns>TcpClientConnectionInput with user's choices</returns>
        public static TcpClientConnectionInput Show(IApplication app, TcpClientConnectionSettings currentSettings)
        {
            var result = new TcpClientConnectionInput { WasCancelled = true };

            var hostTextField = new TextField { X = 15, Y = 1, Width = 35, Text = currentSettings.Host };
            var portNumberTextField = new TextField { X = 25, Y = 3, Width = 25, Text = currentSettings.PortNumber.ToString() };
            var baudRateTextField = new TextField { X = 25, Y = 5, Width = 25, Text = currentSettings.BaudRate.ToString() };
            var replyTimeoutTextField = new TextField { X = 25, Y = 7, Width = 25, Text = currentSettings.ReplyTimeout.ToString() };

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
                result.Host = hostTextField.Text;
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

            var dialog = new Dialog { Title = "Start TCP Client Connection", Width = 60, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Host Name:" }, hostTextField,
                      new Label { X = 1, Y = 3, Text = "Port Number:" }, portNumberTextField,
                      new Label { X = 1, Y = 5, Text = "Baud Rate:" }, baudRateTextField,
                      new Label { X = 1, Y = 7, Text = "Reply Timeout(ms):" }, replyTimeoutTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(startButton);
            hostTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
