using System;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting get PIV data parameters and device selection
    /// </summary>
    public static class GetPIVDataDialog
    {
        // Static fields to persist values between calls
        private static string _lastObjectId = "5FC105";
        private static string _lastElementId = "70";
        private static string _lastDataOffset = "00";

        /// <summary>
        /// Shows the get PIV data dialog and returns user input
        /// </summary>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>GetPIVDataInput with user's choices</returns>
        public static GetPIVDataInput Show(DeviceSetting[] devices, string[] deviceList)
        {
            var result = new GetPIVDataInput { WasCancelled = true };

            // Create controls with persisted values
            var objectIdTextField = new TextField(23, 1, 15, _lastObjectId);
            var elementIdTextField = new TextField(23, 3, 15, _lastElementId);
            var dataOffsetTextField = new TextField(23, 5, 15, _lastDataOffset);

            void NextButtonClicked()
            {
                // Validate Object ID (must be 3 bytes / 6 hex characters)
                var objectIdText = objectIdTextField.Text.ToString()!.Trim();
                if (string.IsNullOrEmpty(objectIdText))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "No Object ID entered!", "OK");
                    return;
                }

                byte[] objectId;
                try
                {
                    objectId = Convert.FromHexString(objectIdText);
                    if (objectId.Length != 3)
                    {
                        MessageBox.ErrorQuery(40, 10, "Error", "Object ID must be exactly 3 bytes (6 hex chars)!", "OK");
                        return;
                    }
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid hex characters in Object ID!", "OK");
                    return;
                }

                // Validate Element ID (must be 1 byte / 2 hex characters)
                var elementIdText = elementIdTextField.Text.ToString()!.Trim();
                if (string.IsNullOrEmpty(elementIdText))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "No Element ID entered!", "OK");
                    return;
                }

                byte elementId;
                try
                {
                    var elementIdBytes = Convert.FromHexString(elementIdText);
                    if (elementIdBytes.Length != 1)
                    {
                        MessageBox.ErrorQuery(40, 10, "Error", "Element ID must be exactly 1 byte (2 hex chars)!", "OK");
                        return;
                    }
                    elementId = elementIdBytes[0];
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid hex characters in Element ID!", "OK");
                    return;
                }

                // Validate Data Offset (must be 1 byte / 2 hex characters)
                var dataOffsetText = dataOffsetTextField.Text.ToString()!.Trim();
                if (string.IsNullOrEmpty(dataOffsetText))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "No Data Offset entered!", "OK");
                    return;
                }

                byte dataOffset;
                try
                {
                    var dataOffsetBytes = Convert.FromHexString(dataOffsetText);
                    if (dataOffsetBytes.Length != 1)
                    {
                        MessageBox.ErrorQuery(40, 10, "Error", "Data Offset must be exactly 1 byte (2 hex chars)!", "OK");
                        return;
                    }
                    dataOffset = dataOffsetBytes[0];
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid hex characters in Data Offset!", "OK");
                    return;
                }

                // Save values for next time (after validation succeeds)
                _lastObjectId = objectIdText;
                _lastElementId = elementIdText;
                _lastDataOffset = dataOffsetText;

                Application.RequestStop();

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show("Get PIV Data", devices, deviceList);

                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.ObjectId = objectId;
                    result.ElementId = elementId;
                    result.DataOffset = dataOffset;
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

            var dialog = new Dialog("Get PIV Data", 60, 11, cancelButton, nextButton);
            dialog.Add(new Label(1, 1, "Object ID (hex):"), objectIdTextField,
                      new Label(1, 3, "Element ID (hex):"), elementIdTextField,
                      new Label(1, 5, "Data Offset (hex):"), dataOffsetTextField);
            objectIdTextField.SetFocus();

            Application.Run(dialog);

            return result;
        }
    }
}
