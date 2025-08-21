using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting reader text output parameters and device selection
    /// </summary>
    public static class ReaderTextOutputDialog
    {
        /// <summary>
        /// Shows the reader text output dialog and returns user input
        /// </summary>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>ReaderTextOutputInput with user's choices</returns>
        public static ReaderTextOutputInput Show(DeviceSetting[] devices, string[] deviceList)
        {
            var result = new ReaderTextOutputInput { WasCancelled = true };

            // First, collect text output parameters
            var readerNumberTextField = new TextField(25, 1, 25, "0");
            var textTextField = new TextField(25, 3, 40, "Hello World");

            void NextButtonClicked()
            {
                // Validate reader number
                if (!byte.TryParse(readerNumberTextField.Text.ToString(), out var readerNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reader number entered!", "OK");
                    return;
                }

                // Validate text input
                var text = textTextField.Text.ToString();
                if (string.IsNullOrEmpty(text))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Please enter text to display!", "OK");
                    return;
                }

                Application.RequestStop();

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show("Reader Text Output", devices, deviceList);
                
                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.ReaderNumber = readerNumber;
                    result.Text = text;
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

            var dialog = new Dialog("Reader Text Output", 70, 11, cancelButton, nextButton);
            dialog.Add(new Label(1, 1, "Reader Number:"), readerNumberTextField,
                      new Label(1, 3, "Text:"), textTextField);
            readerNumberTextField.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}