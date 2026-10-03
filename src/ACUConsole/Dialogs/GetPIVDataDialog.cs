using System;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>GetPIVDataInput with user's choices</returns>
        public static GetPIVDataInput Show(IApplication app, DeviceSetting[] devices, string[] deviceList)
        {
            var result = new GetPIVDataInput { WasCancelled = true };

            // Create controls with persisted values
            var objectIdTextField = new TextField { X = 23, Y = 1, Width = 15, Text = _lastObjectId };
            var elementIdTextField = new TextField { X = 23, Y = 3, Width = 15, Text = _lastElementId };
            var dataOffsetTextField = new TextField { X = 23, Y = 5, Width = 15, Text = _lastDataOffset };

            void NextButtonClicked()
            {
                // Validate Object ID (must be 3 bytes / 6 hex characters)
                var objectIdText = objectIdTextField.Text.Trim();
                if (string.IsNullOrEmpty(objectIdText))
                {
                    MessageBox.ErrorQuery(app, "Error", "No Object ID entered!", "OK");
                    return;
                }

                byte[] objectId;
                try
                {
                    objectId = Convert.FromHexString(objectIdText);
                    if (objectId.Length != 3)
                    {
                        MessageBox.ErrorQuery(app, "Error", "Object ID must be exactly 3 bytes (6 hex chars)!", "OK");
                        return;
                    }
                }
                catch
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid hex characters in Object ID!", "OK");
                    return;
                }

                // Validate Element ID (must be 1 byte / 2 hex characters)
                var elementIdText = elementIdTextField.Text.Trim();
                if (string.IsNullOrEmpty(elementIdText))
                {
                    MessageBox.ErrorQuery(app, "Error", "No Element ID entered!", "OK");
                    return;
                }

                byte elementId;
                try
                {
                    var elementIdBytes = Convert.FromHexString(elementIdText);
                    if (elementIdBytes.Length != 1)
                    {
                        MessageBox.ErrorQuery(app, "Error", "Element ID must be exactly 1 byte (2 hex chars)!", "OK");
                        return;
                    }
                    elementId = elementIdBytes[0];
                }
                catch
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid hex characters in Element ID!", "OK");
                    return;
                }

                // Validate Data Offset (must be 1 byte / 2 hex characters)
                var dataOffsetText = dataOffsetTextField.Text.Trim();
                if (string.IsNullOrEmpty(dataOffsetText))
                {
                    MessageBox.ErrorQuery(app, "Error", "No Data Offset entered!", "OK");
                    return;
                }

                byte dataOffset;
                try
                {
                    var dataOffsetBytes = Convert.FromHexString(dataOffsetText);
                    if (dataOffsetBytes.Length != 1)
                    {
                        MessageBox.ErrorQuery(app, "Error", "Data Offset must be exactly 1 byte (2 hex chars)!", "OK");
                        return;
                    }
                    dataOffset = dataOffsetBytes[0];
                }
                catch
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid hex characters in Data Offset!", "OK");
                    return;
                }

                // Save values for next time (after validation succeeds)
                _lastObjectId = objectIdText;
                _lastElementId = elementIdText;
                _lastDataOffset = dataOffsetText;

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show(app, "Get PIV Data", devices, deviceList);

                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.ObjectId = objectId;
                    result.ElementId = elementId;
                    result.DataOffset = dataOffset;
                    result.DeviceAddress = deviceSelection.SelectedDeviceAddress;
                    result.WasCancelled = false;
                }

                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                app.RequestStop();
            }

            var nextButton = new Button { Text = "Next", IsDefault = true };
            nextButton.Accepting += (_, e) => { NextButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = "Get PIV Data", Width = 60, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Object ID (hex):" }, objectIdTextField,
                      new Label { X = 1, Y = 3, Text = "Element ID (hex):" }, elementIdTextField,
                      new Label { X = 1, Y = 5, Text = "Data Offset (hex):" }, dataOffsetTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(nextButton);
            objectIdTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
