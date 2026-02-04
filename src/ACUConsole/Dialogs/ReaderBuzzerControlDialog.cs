using ACUConsole.Extensions;
using ACUConsole.Model.DialogInputs;
using OSDP.Net.Model.CommandData;
using Terminal.Gui;

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
        public static ReaderBuzzerControlInput Show()
        {
            var result = new ReaderBuzzerControlInput { WasCancelled = true };

            // Labels: longest is "OFF Time (x100ms):" (18 chars), x = 18 + 2 = 20
            var readerNumberTextField = new TextField(20, 1, 25, "0");

            var toneCodeComboBox = new ComboBox(new Rect(20, 3, 30, 5), ToneCodes)
            {
                SelectedItem = 1
            }.ConfigureForOptimalUX();

            var onTimeTextField = new TextField(20, 5, 25, "2");
            var offTimeTextField = new TextField(20, 7, 25, "2");
            var countTextField = new TextField(20, 9, 25, "1");

            void SendButtonClicked()
            {
                if (!byte.TryParse(readerNumberTextField.Text.ToString(), out var readerNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reader number!", "OK");
                    return;
                }

                if (!byte.TryParse(onTimeTextField.Text.ToString(), out var onTime))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid ON time!", "OK");
                    return;
                }

                if (!byte.TryParse(offTimeTextField.Text.ToString(), out var offTime))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid OFF time!", "OK");
                    return;
                }

                if (!byte.TryParse(countTextField.Text.ToString(), out var count))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid count!", "OK");
                    return;
                }

                result.ReaderNumber = readerNumber;
                result.ToneCode = toneCodeComboBox.SelectedItem == 0 ? ToneCode.Off : ToneCode.Default;
                result.OnTime = onTime;
                result.OffTime = offTime;
                result.Count = count;
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

            var dialog = new Dialog("Reader Buzzer Control", 60, 16, cancelButton, sendButton);
            dialog.Add(
                new Label(1, 1, "Reader Number:"), readerNumberTextField,
                new Label(1, 3, "Tone Code:"), toneCodeComboBox,
                new Label(1, 5, "ON Time (x100ms):"), onTimeTextField,
                new Label(1, 7, "OFF Time (x100ms):"), offTimeTextField,
                new Label(1, 9, "Count:"), countTextField);
            readerNumberTextField.SetFocus();

            Application.Run(dialog);

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
