using ACUConsole.Extensions;
using ACUConsole.Model.DialogInputs;
using OSDP.Net.Model.CommandData;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting reader LED control parameters with full OSDP 2.2.2 support
    /// </summary>
    public static class ReaderLedControlDialog
    {
        private static readonly string[] StandardColors =
        [
            "Black (0x00)",
            "Red (0x01)",
            "Green (0x02)",
            "Amber (0x03)",
            "Blue (0x04)",
            "Magenta (0x05)",
            "Cyan (0x06)",
            "White (0x07)",
            "Custom (hex)"
        ];

        private static readonly string[] TemporaryControlCodes =
        [
            "NOP - Do not alter",
            "Cancel temporary, show permanent",
            "Set temporary and start timer"
        ];

        private static readonly string[] PermanentControlCodes =
        [
            "NOP - Do not alter",
            "Set permanent state"
        ];

        /// <summary>
        /// Shows the reader LED control dialog sequence and returns user input
        /// </summary>
        /// <returns>ReaderLedControlInput with user's choices</returns>
        public static ReaderLedControlInput Show()
        {
            var result = new ReaderLedControlInput { WasCancelled = true };

            // Step 1: Get reader and LED numbers
            if (!ShowBasicSettingsDialog(result))
            {
                return result;
            }

            // Step 2: Get temporary settings
            if (!ShowTemporarySettingsDialog(result))
            {
                return result;
            }

            // Step 3: Get permanent settings
            if (!ShowPermanentSettingsDialog(result))
            {
                return result;
            }

            result.WasCancelled = false;
            return result;
        }

        private static bool ShowBasicSettingsDialog(ReaderLedControlInput result)
        {
            var completed = false;

            // Labels: "Reader Number:" (14 chars), "LED Number:" (11 chars)
            // x = longest_label (14) + 5 = 19, use 20
            var readerNumberTextField = new TextField(20, 1, 25, "0");
            var ledNumberTextField = new TextField(20, 3, 25, "0");

            void NextButtonClicked()
            {
                if (!byte.TryParse(readerNumberTextField.Text.ToString(), out var readerNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reader number!", "OK");
                    return;
                }

                if (!byte.TryParse(ledNumberTextField.Text.ToString(), out var ledNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid LED number!", "OK");
                    return;
                }

                result.ReaderNumber = readerNumber;
                result.LedNumber = ledNumber;
                completed = true;
                Application.RequestStop();
            }

            void CancelButtonClicked()
            {
                Application.RequestStop();
            }

            var nextButton = new Button("Next", true);
            nextButton.Clicked += NextButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("LED Control - Step 1 of 3", 60, 10, cancelButton, nextButton);
            dialog.Add(
                new Label(1, 1, "Reader Number:"), readerNumberTextField,
                new Label(1, 3, "LED Number:"), ledNumberTextField);
            readerNumberTextField.SetFocus();

            Application.Run(dialog);

            return completed;
        }

        private static bool ShowTemporarySettingsDialog(ReaderLedControlInput result)
        {
            var completed = false;

            // Controls at x=20, custom hex fields inline at x=52
            var modeComboBox = new ComboBox(new Rect(20, 1, 30, 5), TemporaryControlCodes)
            {
                SelectedItem = 1
            }.ConfigureForOptimalUX();

            var onTimeTextField = new TextField(20, 3, 15, "1");
            var offTimeTextField = new TextField(20, 5, 15, "0");

            var onColorHexTextField = new TextField(57, 7, 8, "01");
            var onColorComboBox = new ComboBox(new Rect(20, 7, 30, 5), StandardColors)
            {
                SelectedItem = 1
            }.ConfigureForOptimalUX();
            onColorComboBox.SelectedItemChanged += args =>
                onColorHexTextField.Text = IndexToHex(args.Item);

            var offColorHexTextField = new TextField(57, 9, 8, "00");
            var offColorComboBox = new ComboBox(new Rect(20, 9, 30, 5), StandardColors)
            {
                SelectedItem = 0
            }.ConfigureForOptimalUX();
            offColorComboBox.SelectedItemChanged += args =>
                offColorHexTextField.Text = IndexToHex(args.Item);

            var timerTextField = new TextField(20, 11, 15, "0");

            void NextButtonClicked()
            {
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

                if (!ushort.TryParse(timerTextField.Text.ToString(), out var timer))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid timer value!", "OK");
                    return;
                }

                // Parse ON color
                if (!TryGetColorValue(onColorComboBox.SelectedItem, onColorHexTextField.Text.ToString(),
                        out var onColor, out var onColorError))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", onColorError, "OK");
                    return;
                }

                // Parse OFF color
                if (!TryGetColorValue(offColorComboBox.SelectedItem, offColorHexTextField.Text.ToString(),
                        out var offColor, out var offColorError))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", offColorError, "OK");
                    return;
                }

                result.TemporaryMode = (TemporaryReaderControlCode)modeComboBox.SelectedItem;
                result.TemporaryOnTime = onTime;
                result.TemporaryOffTime = offTime;
                result.TemporaryOnColor = onColor;
                result.TemporaryOffColor = offColor;
                result.TemporaryTimer = timer;
                completed = true;
                Application.RequestStop();
            }

            void CancelButtonClicked()
            {
                Application.RequestStop();
            }

            var nextButton = new Button("Next", true);
            nextButton.Clicked += NextButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("LED Control - Temporary (2 of 3)", 70, 18, cancelButton, nextButton);
            dialog.Add(
                new Label(1, 1, "Control Code:"), modeComboBox,
                new Label(1, 3, "ON Time (x100ms):"), onTimeTextField,
                new Label(1, 5, "OFF Time (x100ms):"), offTimeTextField,
                new Label(1, 7, "ON Color:"), onColorComboBox,
                new Label(52, 7, "hex:"), onColorHexTextField,
                new Label(1, 9, "OFF Color:"), offColorComboBox,
                new Label(52, 9, "hex:"), offColorHexTextField,
                new Label(1, 11, "Timer (x100ms):"), timerTextField);
            modeComboBox.SetFocus();

            Application.Run(dialog);

            return completed;
        }

        private static bool ShowPermanentSettingsDialog(ReaderLedControlInput result)
        {
            var completed = false;

            // Controls at x=20, custom hex fields inline at x=52
            var modeComboBox = new ComboBox(new Rect(20, 1, 30, 5), PermanentControlCodes)
            {
                SelectedItem = 1
            }.ConfigureForOptimalUX();

            var onTimeTextField = new TextField(20, 3, 15, "1");
            var offTimeTextField = new TextField(20, 5, 15, "0");

            var onColorHexTextField = new TextField(57, 7, 8, "01");
            var onColorComboBox = new ComboBox(new Rect(20, 7, 30, 5), StandardColors)
            {
                SelectedItem = 1
            }.ConfigureForOptimalUX();
            onColorComboBox.SelectedItemChanged += args =>
                onColorHexTextField.Text = IndexToHex(args.Item);

            var offColorHexTextField = new TextField(57, 9, 8, "00");
            var offColorComboBox = new ComboBox(new Rect(20, 9, 30, 5), StandardColors)
            {
                SelectedItem = 0
            }.ConfigureForOptimalUX();
            offColorComboBox.SelectedItemChanged += args =>
                offColorHexTextField.Text = IndexToHex(args.Item);

            void SendButtonClicked()
            {
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

                // Parse ON color
                if (!TryGetColorValue(onColorComboBox.SelectedItem, onColorHexTextField.Text.ToString(),
                        out var onColor, out var onColorError))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", onColorError, "OK");
                    return;
                }

                // Parse OFF color
                if (!TryGetColorValue(offColorComboBox.SelectedItem, offColorHexTextField.Text.ToString(),
                        out var offColor, out var offColorError))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", offColorError, "OK");
                    return;
                }

                result.PermanentMode = (PermanentReaderControlCode)modeComboBox.SelectedItem;
                result.PermanentOnTime = onTime;
                result.PermanentOffTime = offTime;
                result.PermanentOnColor = onColor;
                result.PermanentOffColor = offColor;
                completed = true;
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

            var dialog = new Dialog("LED Control - Permanent (3 of 3)", 70, 16, cancelButton, sendButton);
            dialog.Add(
                new Label(1, 1, "Control Code:"), modeComboBox,
                new Label(1, 3, "ON Time (x100ms):"), onTimeTextField,
                new Label(1, 5, "OFF Time (x100ms):"), offTimeTextField,
                new Label(1, 7, "ON Color:"), onColorComboBox,
                new Label(52, 7, "hex:"), onColorHexTextField,
                new Label(1, 9, "OFF Color:"), offColorComboBox,
                new Label(52, 9, "hex:"), offColorHexTextField);
            modeComboBox.SetFocus();

            Application.Run(dialog);

            return completed;
        }

        /// <summary>
        /// Converts a color index to a two-digit hex string
        /// </summary>
        /// <param name="index">Color index (0-7 for standard, 8 for custom)</param>
        /// <returns>Two-digit hex string (e.g., "01", "08")</returns>
        private static string IndexToHex(int index)
        {
            // For custom (index 8), default to "08"
            return index.ToString("X2");
        }

        /// <summary>
        /// Gets the color byte value from ComboBox selection and custom hex field
        /// </summary>
        /// <param name="selectedIndex">Index selected in the color ComboBox</param>
        /// <param name="hexValue">Value from the custom hex TextField</param>
        /// <param name="color">Output color byte value</param>
        /// <param name="errorMessage">Error message if parsing fails</param>
        /// <returns>True if color value was successfully parsed</returns>
        private static bool TryGetColorValue(int selectedIndex, string hexValue, out byte color, out string errorMessage)
        {
            color = 0;
            errorMessage = string.Empty;

            // If a standard color is selected (0-7), use the index directly
            if (selectedIndex >= 0 && selectedIndex <= 7)
            {
                color = (byte)selectedIndex;
                return true;
            }

            // Custom color selected (index 8) - parse hex value
            if (string.IsNullOrWhiteSpace(hexValue))
            {
                errorMessage = "Custom color hex value is required!";
                return false;
            }

            // Remove 0x prefix if present
            var cleanHex = hexValue.Trim();
            if (cleanHex.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
            {
                cleanHex = cleanHex[2..];
            }

            try
            {
                var value = System.Convert.ToByte(cleanHex, 16);
                color = value;
                return true;
            }
            catch
            {
                errorMessage = "Invalid hex color value! Use 00-FF.";
                return false;
            }
        }

        /// <summary>
        /// Gets the description for an LED compliance level
        /// </summary>
        /// <param name="complianceLevel">The compliance level from device capabilities</param>
        /// <returns>Human-readable description of the compliance level</returns>
        public static string GetComplianceLevelDescription(byte complianceLevel)
        {
            return complianceLevel switch
            {
                0 => "Not supported",
                1 => "On/off control only; Colors: Black, Red",
                2 => "Timed commands; Colors: Black, Red",
                3 => "Timed + bi-color; Colors: Black, Red, Green",
                4 => "Timed + tri-color; Colors: Black, Red, Green, Amber",
                5 => "Timed + RGB; Colors: Black through White (0-7)",
                6 => "Timed + RGB + Custom; Colors: Any (0-255)",
                _ => $"Unknown compliance level: {complianceLevel}"
            };
        }
    }
}
