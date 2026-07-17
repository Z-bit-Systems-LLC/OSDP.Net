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
        public static ReaderLedControlInput Show(IApplication app)
        {
            var result = new ReaderLedControlInput { WasCancelled = true };

            // Step 1: Get reader and LED numbers
            if (!ShowBasicSettingsDialog(app, result))
            {
                return result;
            }

            // Step 2: Get temporary settings
            if (!ShowTemporarySettingsDialog(app, result))
            {
                return result;
            }

            // Step 3: Get permanent settings
            if (!ShowPermanentSettingsDialog(app, result))
            {
                return result;
            }

            result.WasCancelled = false;
            return result;
        }

        private static bool ShowBasicSettingsDialog(IApplication app, ReaderLedControlInput result)
        {
            var completed = false;

            // Labels: "Reader Number:" (14 chars), "LED Number:" (11 chars)
            // x = longest_label (14) + 5 = 19, use 20
            var readerNumberTextField = new TextField { X = 20, Y = 1, Width = 25, Text = "0" };
            var ledNumberTextField = new TextField { X = 20, Y = 3, Width = 25, Text = "0" };

            void NextButtonClicked()
            {
                if (!byte.TryParse(readerNumberTextField.Text, out var readerNumber))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid reader number!", "OK");
                    return;
                }

                if (!byte.TryParse(ledNumberTextField.Text, out var ledNumber))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid LED number!", "OK");
                    return;
                }

                result.ReaderNumber = readerNumber;
                result.LedNumber = ledNumber;
                completed = true;
                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                app.RequestStop();
            }

            var nextButton = new Button { Text = "Next", IsDefault = true };
            nextButton.Accepting += (_, e) => { NextButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = "LED Control - Step 1 of 3", Width = 60, Height = Dim.Auto() };
            dialog.Add(
                new Label { X = 1, Y = 1, Text = "Reader Number:" }, readerNumberTextField,
                new Label { X = 1, Y = 3, Text = "LED Number:" }, ledNumberTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(nextButton);
            readerNumberTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return completed;
        }

        private static bool ShowTemporarySettingsDialog(IApplication app, ReaderLedControlInput result)
        {
            var completed = false;

            // Controls at x=20, custom hex fields inline at x=52
            var modeComboBox = new DropDownList
            {
                X = 20,
                Y = 1,
                Width = 30,
                Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(TemporaryControlCodes))
            }.ConfigureForOptimalUX();
            modeComboBox.Text = TemporaryControlCodes[1];

            var onTimeTextField = new TextField { X = 20, Y = 3, Width = 15, Text = "1" };
            var offTimeTextField = new TextField { X = 20, Y = 5, Width = 15, Text = "0" };

            var onColorHexTextField = new TextField { X = 57, Y = 7, Width = 8, Text = "01" };
            var onColorComboBox = new DropDownList
            {
                X = 20,
                Y = 7,
                Width = 30,
                Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(StandardColors))
            }.ConfigureForOptimalUX();
            onColorComboBox.Text = StandardColors[1];
            onColorComboBox.TextChanged += (_, _) =>
                onColorHexTextField.Text = IndexToHex(Array.IndexOf(StandardColors, onColorComboBox.Text));

            var offColorHexTextField = new TextField { X = 57, Y = 9, Width = 8, Text = "00" };
            var offColorComboBox = new DropDownList
            {
                X = 20,
                Y = 9,
                Width = 30,
                Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(StandardColors))
            }.ConfigureForOptimalUX();
            offColorComboBox.Text = StandardColors[0];
            offColorComboBox.TextChanged += (_, _) =>
                offColorHexTextField.Text = IndexToHex(Array.IndexOf(StandardColors, offColorComboBox.Text));

            var timerTextField = new TextField { X = 20, Y = 11, Width = 15, Text = "0" };

            void NextButtonClicked()
            {
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

                if (!ushort.TryParse(timerTextField.Text, out var timer))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid timer value!", "OK");
                    return;
                }

                // Parse ON color
                if (!TryGetColorValue(Array.IndexOf(StandardColors, onColorComboBox.Text), onColorHexTextField.Text,
                        out var onColor, out var onColorError))
                {
                    MessageBox.ErrorQuery(app, "Error", onColorError, "OK");
                    return;
                }

                // Parse OFF color
                if (!TryGetColorValue(Array.IndexOf(StandardColors, offColorComboBox.Text), offColorHexTextField.Text,
                        out var offColor, out var offColorError))
                {
                    MessageBox.ErrorQuery(app, "Error", offColorError, "OK");
                    return;
                }

                result.TemporaryMode = (TemporaryReaderControlCode)Array.IndexOf(TemporaryControlCodes, modeComboBox.Text);
                result.TemporaryOnTime = onTime;
                result.TemporaryOffTime = offTime;
                result.TemporaryOnColor = onColor;
                result.TemporaryOffColor = offColor;
                result.TemporaryTimer = timer;
                completed = true;
                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                app.RequestStop();
            }

            var nextButton = new Button { Text = "Next", IsDefault = true };
            nextButton.Accepting += (_, e) => { NextButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = "LED Control - Temporary (2 of 3)", Width = 70, Height = Dim.Auto() };
            dialog.Add(
                new Label { X = 1, Y = 1, Text = "Control Code:" }, modeComboBox,
                new Label { X = 1, Y = 3, Text = "ON Time (x100ms):" }, onTimeTextField,
                new Label { X = 1, Y = 5, Text = "OFF Time (x100ms):" }, offTimeTextField,
                new Label { X = 1, Y = 7, Text = "ON Color:" }, onColorComboBox,
                new Label { X = 52, Y = 7, Text = "hex:" }, onColorHexTextField,
                new Label { X = 1, Y = 9, Text = "OFF Color:" }, offColorComboBox,
                new Label { X = 52, Y = 9, Text = "hex:" }, offColorHexTextField,
                new Label { X = 1, Y = 11, Text = "Timer (x100ms):" }, timerTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(nextButton);
            modeComboBox.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return completed;
        }

        private static bool ShowPermanentSettingsDialog(IApplication app, ReaderLedControlInput result)
        {
            var completed = false;

            // Controls at x=20, custom hex fields inline at x=52
            var modeComboBox = new DropDownList
            {
                X = 20,
                Y = 1,
                Width = 30,
                Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(PermanentControlCodes))
            }.ConfigureForOptimalUX();
            modeComboBox.Text = PermanentControlCodes[1];

            var onTimeTextField = new TextField { X = 20, Y = 3, Width = 15, Text = "1" };
            var offTimeTextField = new TextField { X = 20, Y = 5, Width = 15, Text = "0" };

            var onColorHexTextField = new TextField { X = 57, Y = 7, Width = 8, Text = "01" };
            var onColorComboBox = new DropDownList
            {
                X = 20,
                Y = 7,
                Width = 30,
                Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(StandardColors))
            }.ConfigureForOptimalUX();
            onColorComboBox.Text = StandardColors[1];
            onColorComboBox.TextChanged += (_, _) =>
                onColorHexTextField.Text = IndexToHex(Array.IndexOf(StandardColors, onColorComboBox.Text));

            var offColorHexTextField = new TextField { X = 57, Y = 9, Width = 8, Text = "00" };
            var offColorComboBox = new DropDownList
            {
                X = 20,
                Y = 9,
                Width = 30,
                Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(StandardColors))
            }.ConfigureForOptimalUX();
            offColorComboBox.Text = StandardColors[0];
            offColorComboBox.TextChanged += (_, _) =>
                offColorHexTextField.Text = IndexToHex(Array.IndexOf(StandardColors, offColorComboBox.Text));

            void SendButtonClicked()
            {
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

                // Parse ON color
                if (!TryGetColorValue(Array.IndexOf(StandardColors, onColorComboBox.Text), onColorHexTextField.Text,
                        out var onColor, out var onColorError))
                {
                    MessageBox.ErrorQuery(app, "Error", onColorError, "OK");
                    return;
                }

                // Parse OFF color
                if (!TryGetColorValue(Array.IndexOf(StandardColors, offColorComboBox.Text), offColorHexTextField.Text,
                        out var offColor, out var offColorError))
                {
                    MessageBox.ErrorQuery(app, "Error", offColorError, "OK");
                    return;
                }

                result.PermanentMode = (PermanentReaderControlCode)Array.IndexOf(PermanentControlCodes, modeComboBox.Text);
                result.PermanentOnTime = onTime;
                result.PermanentOffTime = offTime;
                result.PermanentOnColor = onColor;
                result.PermanentOffColor = offColor;
                completed = true;
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

            var dialog = new Dialog { Title = "LED Control - Permanent (3 of 3)", Width = 70, Height = Dim.Auto() };
            dialog.Add(
                new Label { X = 1, Y = 1, Text = "Control Code:" }, modeComboBox,
                new Label { X = 1, Y = 3, Text = "ON Time (x100ms):" }, onTimeTextField,
                new Label { X = 1, Y = 5, Text = "OFF Time (x100ms):" }, offTimeTextField,
                new Label { X = 1, Y = 7, Text = "ON Color:" }, onColorComboBox,
                new Label { X = 52, Y = 7, Text = "hex:" }, onColorHexTextField,
                new Label { X = 1, Y = 9, Text = "OFF Color:" }, offColorComboBox,
                new Label { X = 52, Y = 9, Text = "hex:" }, offColorHexTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(sendButton);
            modeComboBox.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

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
            if (cleanHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                cleanHex = cleanHex[2..];
            }

            try
            {
                var value = Convert.ToByte(cleanHex, 16);
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
