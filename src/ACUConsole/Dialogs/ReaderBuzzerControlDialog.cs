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
    /// Dialog for collecting reader buzzer control parameters with full OSDP 2.2.2 support
    /// </summary>
    public static class ReaderBuzzerControlDialog
    {
        private static readonly string[] ToneCodes =
        [
            "Off (0x01) - Turn off tone",
            "Default (0x02) - Default tone"
        ];

        /// <summary>
        /// Shows the reader buzzer control dialog and returns user input
        /// </summary>
        /// <returns>ReaderBuzzerControlInput with user's choices</returns>
        public static ReaderBuzzerControlInput Show(IApplication app)
        {
            var result = new ReaderBuzzerControlInput { WasCancelled = true };

            // Labels: longest is "OFF Time (x100ms):" (18 chars), x = 18 + 2 = 20
            var readerNumberTextField = new TextField { X = 20, Y = 1, Width = 25, Text = "0" };

            var toneCodeComboBox = new DropDownList
            {
                X = 20,
                Y = 3,
                Width = 30,
                Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(ToneCodes))
            }.ConfigureForOptimalUX();
            toneCodeComboBox.Text = ToneCodes[1];

            var onTimeTextField = new TextField { X = 20, Y = 5, Width = 25, Text = "2" };
            var offTimeTextField = new TextField { X = 20, Y = 7, Width = 25, Text = "2" };
            var countTextField = new TextField { X = 20, Y = 9, Width = 25, Text = "1" };

            void SendButtonClicked()
            {
                if (!byte.TryParse(readerNumberTextField.Text, out var readerNumber))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid reader number!", "OK");
                    return;
                }

                if (!byte.TryParse(onTimeTextField.Text, out var onTime))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid ON time!", "OK");
                    return;
                }

                if (!byte.TryParse(offTimeTextField.Text, out var offTime))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid OFF time!", "OK");
                    return;
                }

                if (!byte.TryParse(countTextField.Text, out var count))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid count!", "OK");
                    return;
                }

                result.ReaderNumber = readerNumber;
                result.ToneCode = Array.IndexOf(ToneCodes, toneCodeComboBox.Text) == 0 ? ToneCode.Off : ToneCode.Default;
                result.OnTime = onTime;
                result.OffTime = offTime;
                result.Count = count;
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

            var dialog = new Dialog { Title = "Reader Buzzer Control", Width = 60, Height = Dim.Auto() };
            dialog.Add(
                new Label { X = 1, Y = 1, Text = "Reader Number:" }, readerNumberTextField,
                new Label { X = 1, Y = 3, Text = "Tone Code:" }, toneCodeComboBox,
                new Label { X = 1, Y = 5, Text = "ON Time (x100ms):" }, onTimeTextField,
                new Label { X = 1, Y = 7, Text = "OFF Time (x100ms):" }, offTimeTextField,
                new Label { X = 1, Y = 9, Text = "Count:" }, countTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(sendButton);
            readerNumberTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }

        /// <summary>
        /// Gets the description for a buzzer compliance level
        /// </summary>
        /// <param name="complianceLevel">The compliance level from device capabilities</param>
        /// <returns>Human-readable description of the compliance level</returns>
        public static string GetComplianceLevelDescription(byte complianceLevel)
        {
            return complianceLevel switch
            {
                0 => "Not supported",
                1 => "On/off control only",
                2 => "Timed operation supported",
                _ => $"Unknown compliance level: {complianceLevel}"
            };
        }
    }
}
