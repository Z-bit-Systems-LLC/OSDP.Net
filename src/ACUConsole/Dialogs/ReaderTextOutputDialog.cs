using ACUConsole.Extensions;
using ACUConsole.Model.DialogInputs;
using OSDP.Net.Model.CommandData;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting reader text output parameters with full OSDP 2.2 support
    /// </summary>
    public static class ReaderTextOutputDialog
    {
        private static readonly string[] TextCommands =
        [
            "Permanent - No Wrap (0x01)",
            "Permanent - With Wrap (0x02)",
            "Temporary - No Wrap (0x03)",
            "Temporary - With Wrap (0x04)"
        ];

        /// <summary>
        /// Shows the reader text output dialog and returns user input
        /// </summary>
        /// <returns>ReaderTextOutputInput with user's choices</returns>
        public static ReaderTextOutputInput Show()
        {
            var result = new ReaderTextOutputInput { WasCancelled = true };

            // Labels: longest is "Temp Time (x100ms):" (20 chars), x = 25
            var readerNumberTextField = new TextField(25, 1, 15, "0");

            var textCommandComboBox = new ComboBox(new Rect(25, 3, 30, 5), TextCommands)
            {
                SelectedItem = 0
            }.ConfigureForOptimalUX();

            var tempTimeTextField = new TextField(25, 5, 15, "0") { Enabled = false };

            textCommandComboBox.SelectedItemChanged += args =>
            {
                // Enable temporary text time only for temporary commands (index 2 and 3)
                tempTimeTextField.Enabled = args.Item >= 2;
                if (!tempTimeTextField.Enabled)
                {
                    tempTimeTextField.Text = "0";
                }
            };

            var rowTextField = new TextField(25, 7, 15, "1");
            var columnTextField = new TextField(25, 9, 15, "1");
            var textTextField = new TextField(25, 11, 40, "Hello World");

            void SendButtonClicked()
            {
                if (!byte.TryParse(readerNumberTextField.Text.ToString(), out var readerNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reader number!", "OK");
                    return;
                }

                if (!byte.TryParse(tempTimeTextField.Text.ToString(), out var tempTime))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid temporary text time!", "OK");
                    return;
                }

                if (!byte.TryParse(rowTextField.Text.ToString(), out var row) || row < 1)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid row! Must be 1 or greater.", "OK");
                    return;
                }

                if (!byte.TryParse(columnTextField.Text.ToString(), out var column) || column < 1)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid column! Must be 1 or greater.", "OK");
                    return;
                }

                var text = textTextField.Text.ToString();
                if (string.IsNullOrEmpty(text))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Please enter text to display!", "OK");
                    return;
                }

                result.ReaderNumber = readerNumber;
                result.TextCommand = (TextCommand)(textCommandComboBox.SelectedItem + 1);
                result.TemporaryTextTime = tempTime;
                result.Row = row;
                result.Column = column;
                result.Text = text;
                result.WasCancelled = false;
                Application.RequestStop();
            }

            void CancelButtonClicked()
            {
                Application.RequestStop();
            }

            var sendButton = new Button("Send", true);
            sendButton.Clicked += SendButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("Reader Text Output", 70, 18, cancelButton, sendButton);
            dialog.Add(
                new Label(1, 1, "Reader Number:"), readerNumberTextField,
                new Label(1, 3, "Text Command:"), textCommandComboBox,
                new Label(1, 5, "Temp Time (x100ms):"), tempTimeTextField,
                new Label(1, 7, "Row:"), rowTextField,
                new Label(1, 9, "Column:"), columnTextField,
                new Label(1, 11, "Text:"), textTextField);
            readerNumberTextField.SetFocus();

            Application.Run(dialog);

            return result;
        }

        /// <summary>
        /// Gets the description for a text output compliance level
        /// </summary>
        /// <param name="complianceLevel">The compliance level from device capabilities</param>
        /// <returns>Human-readable description of the compliance level</returns>
        public static string GetComplianceLevelDescription(byte complianceLevel)
        {
            return complianceLevel switch
            {
                0 => "Not supported",
                1 => "Text output supported (single line, no wrap)",
                2 => "Multi-line text output supported",
                _ => $"Unknown compliance level: {complianceLevel}"
            };
        }
    }
}
