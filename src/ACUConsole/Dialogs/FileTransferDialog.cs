using System;
using System.Collections.Generic;
using System.IO;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

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
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>FileTransferInput with user's choices</returns>
        public static FileTransferInput Show(DeviceSetting[] devices, string[] deviceList)
        {
            var result = new FileTransferInput { WasCancelled = true };

            // First, collect file transfer parameters
            var typeTextField = new TextField(25, 1, 25, "1");
            var messageSizeTextField = new TextField(25, 3, 25, "128");
            var filePathTextField = new TextField(25, 5, 40, "");

            void BrowseFileButtonClicked()
            {
                var openDialog = new OpenDialog("Select File to Transfer", "", new List<string>());
                Application.Run(openDialog);

                if (!openDialog.Canceled && !string.IsNullOrEmpty(openDialog.FilePath?.ToString()))
                {
                    filePathTextField.Text = openDialog.FilePath.ToString();
                }
            }

            void NextButtonClicked()
            {
                if (!byte.TryParse(typeTextField.Text.ToString(), out var type))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid type entered!", "OK");
                    return;
                }

                if (!byte.TryParse(messageSizeTextField.Text.ToString(), out var messageSize))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid message size entered!", "OK");
                    return;
                }

                var filePath = filePathTextField.Text.ToString();
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Please enter file path!", "OK");
                    return;
                }

                byte[] fileData;
                try
                {
                    if (!File.Exists(filePath))
                    {
                        MessageBox.ErrorQuery(40, 10, "Error", "File does not exist!", "OK");
                        return;
                    }
                    fileData = File.ReadAllBytes(filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(60, 10, "Error", $"Failed to read file: {ex.Message}", "OK");
                    return;
                }

                Application.RequestStop();

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show("File Transfer", devices, deviceList);
                
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
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                Application.RequestStop();
            }

            var sendButton = new Button("Next", true);
            sendButton.Clicked += NextButtonClicked;
            var browseButton = new Button("Browse");
            browseButton.Clicked += BrowseFileButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("File Transfer", 80, 15, cancelButton, sendButton);
            dialog.Add(new Label(1, 1, "Type:"), typeTextField,
                      new Label(1, 3, "Message Size:"), messageSizeTextField,
                      new Label(1, 5, "File Path:"), filePathTextField);
            
            browseButton.X = Pos.Right(filePathTextField) + 2;
            browseButton.Y = 5;
            dialog.Add(browseButton);
            
            typeTextField.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}