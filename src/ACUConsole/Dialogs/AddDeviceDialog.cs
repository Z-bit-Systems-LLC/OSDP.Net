using System;
using System.Linq;
using ACUConsole.Configuration;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting device addition parameters
    /// </summary>
    public static class AddDeviceDialog
    {
        /// <summary>
        /// Shows the add device dialog and returns user input
        /// </summary>
        /// <param name="existingDevices">List of existing devices to check for duplicates</param>
        /// <returns>AddDeviceInput with user's choices</returns>
        public static AddDeviceInput Show(DeviceSetting[] existingDevices)
        {
            var result = new AddDeviceInput { WasCancelled = true };

            var nameTextField = new TextField(15, 1, 35, string.Empty);
            var addressTextField = new TextField(15, 3, 35, string.Empty);
            var useCrcCheckBox = new CheckBox(1, 5, "Use CRC", true);
            var useSecureChannelCheckBox = new CheckBox(1, 6, "Use Secure Channel", true);
            var keyTextField = new TextField(15, 8, 35, Convert.ToHexString(DeviceSetting.DefaultKey));

            void AddDeviceButtonClicked()
            {
                // Validate address
                if (!byte.TryParse(addressTextField.Text.ToString(), out var address) || address > 127)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid address entered!", "OK");
                    return;
                }

                // Validate key length
                if (keyTextField.Text == null || keyTextField.Text.Length != 32)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid key length entered!", "OK");
                    return;
                }

                // Validate hex key format
                byte[] key;
                try
                {
                    key = Convert.FromHexString(keyTextField.Text.ToString()!);
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid hex characters!", "OK");
                    return;
                }

                // Check for existing device at address
                var existingDevice = existingDevices.FirstOrDefault(d => d.Address == address);
                bool overwriteExisting = false;
                if (existingDevice != null)
                {
                    if (MessageBox.Query(60, 10, "Overwrite", "Device already exists at that address, overwrite?", 1, "No", "Yes") == 0)
                    {
                        return;
                    }
                    overwriteExisting = true;
                }

                // All validation passed - collect the data
                result.Name = nameTextField.Text.ToString();
                result.Address = address;
                result.UseCrc = useCrcCheckBox.Checked;
                result.UseSecureChannel = useSecureChannelCheckBox.Checked;
                result.SecureChannelKey = key;
                result.OverwriteExisting = overwriteExisting;
                result.WasCancelled = false;
                
                Application.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                Application.RequestStop();
            }

            var addButton = new Button("Add", true);
            addButton.Clicked += AddDeviceButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("Add Device", 60, 13, cancelButton, addButton);
            dialog.Add(new Label(1, 1, "Name:"), nameTextField,
                      new Label(1, 3, "Address:"), addressTextField,
                      useCrcCheckBox,
                      useSecureChannelCheckBox,
                      new Label(1, 8, "Secure Key:"), keyTextField);
            nameTextField.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}