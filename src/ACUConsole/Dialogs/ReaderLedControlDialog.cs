using ACUConsole.Configuration;
using ACUConsole.Extensions;
using ACUConsole.Model.DialogInputs;
using OSDP.Net.Model.CommandData;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting reader LED control parameters and device selection
    /// </summary>
    public static class ReaderLedControlDialog
    {
        /// <summary>
        /// Shows the reader LED control dialog and returns user input
        /// </summary>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>ReaderLedControlInput with user's choices</returns>
        public static ReaderLedControlInput Show(DeviceSetting[] devices, string[] deviceList)
        {
            var result = new ReaderLedControlInput { WasCancelled = true };

            // First, collect LED control parameters
            var ledNumberTextField = new TextField(25, 1, 25, "0");
            // IMPORTANT: Width must be at least ComboBoxExtensions.MinimumRecommendedWidth (30)
            // for dropdown list to display correctly. See ComboBoxExtensions documentation.
            var colorComboBox = new ComboBox(new Rect(25, 3, 30, 8), new[] { "Black", "Red", "Green", "Amber", "Blue", "Magenta", "Cyan", "White" })
            {
                SelectedItem = 1 // Default to Red
            }.ConfigureForOptimalUX();

            void NextButtonClicked()
            {
                // Validate LED number
                if (!byte.TryParse(ledNumberTextField.Text.ToString(), out var ledNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid LED number entered!", "OK");
                    return;
                }

                var selectedColor = (LedColor)colorComboBox.SelectedItem;
                Application.RequestStop();

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show("Reader LED Control", devices, deviceList);
                
                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.LedNumber = ledNumber;
                    result.Color = selectedColor;
                    result.DeviceAddress = deviceSelection.SelectedDeviceAddress;
                    result.WasCancelled = false;
                }
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                Application.RequestStop();
            }

            var nextButton = new Button("Next", true);
            nextButton.Clicked += NextButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("Reader LED Control", 60, 12, cancelButton, nextButton);
            dialog.Add(new Label(1, 1, "LED Number:"), ledNumberTextField,
                      new Label(1, 3, "Color:"), colorComboBox);
            ledNumberTextField.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}