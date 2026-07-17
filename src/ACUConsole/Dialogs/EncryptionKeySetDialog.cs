using System;
using System.Security.Cryptography;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="devices">Available devices for selection</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>EncryptionKeySetInput with user's choices</returns>
        public static EncryptionKeySetInput Show(IApplication app, DeviceSetting[] devices, string[] deviceList)
        {
            var result = new EncryptionKeySetInput { WasCancelled = true };

            // First, collect encryption key
            var keyTextField = new TextField { X = 1, Y = 3, Width = 35, Text = "" };

            void NextButtonClicked()
            {
                var keyStr = keyTextField.Text;
                if (string.IsNullOrWhiteSpace(keyStr))
                {
                    MessageBox.ErrorQuery(app, "Error", "Please enter encryption key!", "OK");
                    return;
                }

                byte[] key;
                try
                {
                    key = Convert.FromHexString(keyStr);
                }
                catch
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid hex format!", "OK");
                    return;
                }

                if (key.Length != 16)
                {
                    MessageBox.ErrorQuery(app, "Error", "Key must be exactly 16 bytes (32 hex chars)!", "OK");
                    return;
                }

                // Show device selection dialog
                var deviceSelection = DeviceSelectionDialog.Show(app, "Encryption Key Set", devices, deviceList);

                if (!deviceSelection.WasCancelled)
                {
                    // All validation passed - collect the data
                    result.EncryptionKey = key;
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

            void RandomKeyButtonClicked()
            {
                keyTextField.Text = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            }

            var sendButton = new Button { Text = "Next", IsDefault = true };
            sendButton.Accepting += (_, e) => { NextButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };
            var randomButton = new Button { Text = "Random Key" };
            randomButton.Accepting += (_, e) => { RandomKeyButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = "Encryption Key Set", Width = 60, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Encryption Key (16 bytes hex):" }, keyTextField,
                      new Label { X = 1, Y = 5, Text = "Example: '0102030405060708090A0B0C0D0E0F10'" });
            dialog.AddButton(cancelButton);
            dialog.AddButton(randomButton);
            dialog.AddButton(sendButton);
            keyTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
