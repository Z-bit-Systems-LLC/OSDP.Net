using System;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting encryption key set parameters and device selection
    /// </summary>
    public static class EncryptionKeySetDialog
    {
        /// <summary>
        /// Shows the encryption key set dialog and returns user input
        /// </summary>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>EncryptionKeySetInput with user's choices</returns>
        public static EncryptionKeySetInput Show(DeviceSetting[] devices, string[] deviceList)
        {
            var result = new EncryptionKeySetInput { WasCancelled = true };

            // First, collect encryption key
            var keyTextField = new TextField(1, 3, 35, "");

            void NextButtonClicked()
            {
                var keyStr = keyTextField.Text.ToString();
                if (string.IsNullOrWhiteSpace(keyStr))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Please enter encryption key!", "OK");
                    return;
                }

                byte[] key;
                try
                {
                    key = Convert.FromHexString(keyStr);
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid hex format!", "OK");
                    return;
                }

                if (key.Length != 16)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Key must be exactly 16 bytes (32 hex chars)!", "OK");
                    return;
                }

                Application.RequestStop();

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show("Encryption Key Set", devices, deviceList);
                
                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.EncryptionKey = key;
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
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("Encryption Key Set", 60, 12, cancelButton, sendButton);
            dialog.Add(new Label(1, 1, "Encryption Key (16 bytes hex):"), keyTextField,
                      new Label(1, 5, "Example: '0102030405060708090A0B0C0D0E0F10'"));
            keyTextField.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}