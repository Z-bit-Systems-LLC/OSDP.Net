using System;
using System.IO;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting file transfer parameters and device selection
    /// </summary>
    public static class FileTransferDialog
    {
        /// <summary>
        /// Shows the file transfer dialog and returns user input
        /// </summary>
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>FileTransferInput with user's choices</returns>
        public static FileTransferInput Show(IApplication app, DeviceSetting[] devices, string[] deviceList)
        {
            var result = new FileTransferInput { WasCancelled = true };

            // First, collect file transfer parameters
            var typeTextField = new TextField { X = 25, Y = 1, Width = 25, Text = "1" };
            var messageSizeTextField = new TextField { X = 25, Y = 3, Width = 25, Text = "128" };
            var filePathTextField = new TextField { X = 25, Y = 5, Width = 40, Text = "" };

            void BrowseFileButtonClicked()
            {
                var openDialog = new OpenDialog { Title = "Select File to Transfer" };
                app.Run(openDialog);

                if (!openDialog.Canceled && !string.IsNullOrEmpty(openDialog.Path))
                {
                    filePathTextField.Text = openDialog.Path;
                }

                openDialog.Dispose();
            }

            void NextButtonClicked()
            {
                if (!byte.TryParse(typeTextField.Text, out var type))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid type entered!", "OK");
                    return;
                }

                if (!byte.TryParse(messageSizeTextField.Text, out var messageSize))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid message size entered!", "OK");
                    return;
                }

                var filePath = filePathTextField.Text;
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    MessageBox.ErrorQuery(app, "Error", "Please enter file path!", "OK");
                    return;
                }

                byte[] fileData;
                try
                {
                    if (!File.Exists(filePath))
                    {
                        MessageBox.ErrorQuery(app, "Error", "File does not exist!", "OK");
                        return;
                    }
                    fileData = File.ReadAllBytes(filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(app, "Error", $"Failed to read file: {ex.Message}", "OK");
                    return;
                }

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show(app, "File Transfer", devices, deviceList);

                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.Type = type;
                    result.MessageSize = messageSize;
                    result.FilePath = filePath;
                    result.FileData = fileData;
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

            var sendButton = new Button { Text = "Next", IsDefault = true };
            sendButton.Accepting += (_, e) => { NextButtonClicked(); e.Handled = true; };
            var browseButton = new Button { Text = "Browse" };
            browseButton.Accepting += (_, e) => { BrowseFileButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = "File Transfer", Width = 80, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Type:" }, typeTextField,
                      new Label { X = 1, Y = 3, Text = "Message Size:" }, messageSizeTextField,
                      new Label { X = 1, Y = 5, Text = "File Path:" }, filePathTextField);

            browseButton.X = Pos.Right(filePathTextField) + 2;
            browseButton.Y = 5;
            dialog.Add(browseButton);

            dialog.AddButton(cancelButton);
            dialog.AddButton(sendButton);

            typeTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
