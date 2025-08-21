using System;
using ACUConsole.Configuration;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting reader buzzer control parameters and device selection
    /// </summary>
    public static class ReaderBuzzerControlDialog
    {
        /// <summary>
        /// Shows the reader buzzer control dialog and returns user input
        /// </summary>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>ReaderBuzzerControlInput with user's choices</returns>
        public static ReaderBuzzerControlInput Show(DeviceSetting[] devices, string[] deviceList)
        {
            var result = new ReaderBuzzerControlInput { WasCancelled = true };

            // First, collect buzzer control parameters
            var readerNumberTextField = new TextField(25, 1, 25, "0");
            var repeatTimesTextField = new TextField(25, 3, 25, "1");

            void NextButtonClicked()
            {
                // Validate reader number
                if (!byte.TryParse(readerNumberTextField.Text.ToString(), out var readerNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reader number entered!", "OK");
                    return;
                }

                // Validate repeat times
                if (!byte.TryParse(repeatTimesTextField.Text.ToString(), out var repeatTimes))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid repeat times entered!", "OK");
                    return;
                }

                Application.RequestStop();

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show("Reader Buzzer Control", devices, deviceList);
                
                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.ReaderNumber = readerNumber;
                    result.RepeatTimes = repeatTimes;
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

            var dialog = new Dialog("Reader Buzzer Control", 60, 11, cancelButton, nextButton);
            dialog.Add(new Label(1, 1, "Reader Number:"), readerNumberTextField,
                      new Label(1, 3, "Repeat Times:"), repeatTimesTextField);
            readerNumberTextField.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}