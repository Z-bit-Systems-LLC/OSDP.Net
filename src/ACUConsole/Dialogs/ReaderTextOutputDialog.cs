using System;
using System.Collections.ObjectModel;
using ACUConsole.Extensions;
using ACUConsole.Model.DialogInputs;
using OSDP.Net.Model.CommandData;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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
        public static ReaderTextOutputInput Show(IApplication app)
        {
            var result = new ReaderTextOutputInput { WasCancelled = true };

            // Labels: longest is "Temp Time (x100ms):" (20 chars), x = 25
            var readerNumberTextField = new TextField { X = 25, Y = 1, Width = 15, Text = "0" };

            var textCommandComboBox = new DropDownList
            {
                X = 25,
                Y = 3,
                Width = 30,
                Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(TextCommands))
            }.ConfigureForOptimalUX();
            textCommandComboBox.Text = TextCommands[0];

            var tempTimeTextField = new TextField { X = 25, Y = 5, Width = 15, Text = "0", Enabled = false };

            textCommandComboBox.TextChanged += (_, _) =>
            {
                var idx = Array.IndexOf(TextCommands, textCommandComboBox.Text);
                // Enable temporary text time only for temporary commands (index 2 and 3)
                tempTimeTextField.Enabled = idx >= 2;
                if (!tempTimeTextField.Enabled)
                {
                    tempTimeTextField.Text = "0";
                }
            };

            var rowTextField = new TextField { X = 25, Y = 7, Width = 15, Text = "1" };
            var columnTextField = new TextField { X = 25, Y = 9, Width = 15, Text = "1" };
            var textTextField = new TextField { X = 25, Y = 11, Width = 40, Text = "Hello World" };

            void SendButtonClicked()
            {
                if (!byte.TryParse(readerNumberTextField.Text, out var readerNumber))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid reader number!", "OK");
                    return;
                }

                if (!byte.TryParse(tempTimeTextField.Text, out var tempTime))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid temporary text time!", "OK");
                    return;
                }

                if (!byte.TryParse(rowTextField.Text, out var row) || row < 1)
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid row! Must be 1 or greater.", "OK");
                    return;
                }

                if (!byte.TryParse(columnTextField.Text, out var column) || column < 1)
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid column! Must be 1 or greater.", "OK");
                    return;
                }

                var text = textTextField.Text;
                if (string.IsNullOrEmpty(text))
                {
                    MessageBox.ErrorQuery(app, "Error", "Please enter text to display!", "OK");
                    return;
                }

                result.ReaderNumber = readerNumber;
                result.TextCommand = (TextCommand)(Array.IndexOf(TextCommands, textCommandComboBox.Text) + 1);
                result.TemporaryTextTime = tempTime;
                result.Row = row;
                result.Column = column;
                result.Text = text;
                result.WasCancelled = false;
                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                app.RequestStop();
            }

            var sendButton = new Button { Text = "Send", IsDefault = true };
            sendButton.Accepting += (_, e) => { SendButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = "Reader Text Output", Width = 70, Height = Dim.Auto() };
            dialog.Add(
                new Label { X = 1, Y = 1, Text = "Reader Number:" }, readerNumberTextField,
                new Label { X = 1, Y = 3, Text = "Text Command:" }, textCommandComboBox,
                new Label { X = 1, Y = 5, Text = "Temp Time (x100ms):" }, tempTimeTextField,
                new Label { X = 1, Y = 7, Text = "Row:" }, rowTextField,
                new Label { X = 1, Y = 9, Text = "Column:" }, columnTextField,
                new Label { X = 1, Y = 11, Text = "Text:" }, textTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(sendButton);
            readerNumberTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

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
