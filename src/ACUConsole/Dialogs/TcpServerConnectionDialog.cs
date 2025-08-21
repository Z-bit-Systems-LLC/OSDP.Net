using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

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
        /// <param name="currentSettings">Current TCP server connection settings for defaults</param>
        /// <returns>TcpServerConnectionInput with user's choices</returns>
        public static TcpServerConnectionInput Show(TcpServerConnectionSettings currentSettings)
        {
            var result = new TcpServerConnectionInput { WasCancelled = true };

            var portNumberTextField = new TextField(25, 1, 25, currentSettings.PortNumber.ToString());
            var baudRateTextField = new TextField(25, 3, 25, currentSettings.BaudRate.ToString());
            var replyTimeoutTextField = new TextField(25, 5, 25, currentSettings.ReplyTimeout.ToString());

            void StartConnectionButtonClicked()
            {
                // Validate port number
                if (!int.TryParse(portNumberTextField.Text.ToString(), out var portNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid port number entered!", "OK");
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
                result.PortNumber = portNumber;
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

            var dialog = new Dialog("Start TCP Server Connection", 60, 12, cancelButton, startButton);
            dialog.Add(new Label(1, 1, "Port Number:"), portNumberTextField,
                      new Label(1, 3, "Baud Rate:"), baudRateTextField,
                      new Label(1, 5, "Reply Timeout(ms):"), replyTimeoutTextField);
            portNumberTextField.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}