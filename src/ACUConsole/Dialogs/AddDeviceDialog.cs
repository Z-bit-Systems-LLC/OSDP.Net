using System;
using System.Linq;
using ACUConsole.Configuration;
using ACUConsole.Extensions;
using ACUConsole.Model.DialogInputs;
using OSDP.Net.Messages.SecureChannel;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting device addition parameters
    /// </summary>
    public static class AddDeviceDialog
    {
        private static readonly byte[] DefaultSC2Key =
        {
            0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47,
            0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F,
            0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57,
            0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F
        };

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

            var scVersionItems = new[] { "V1", "V2" };
            // ComboBox minimum width of 30 per style guide
            var scVersionComboBox = new ComboBox(new Rect(15, 8, 35, 5), scVersionItems);
            scVersionComboBox.SelectedItem = 0;
            scVersionComboBox.ConfigureForOptimalUX();

            var keyTextField = new TextField(15, 10, 35, Convert.ToHexString(DeviceSetting.DefaultKey));

            scVersionComboBox.SelectedItemChanged += args =>
            {
                keyTextField.Text = args.Item == 1
                    ? Convert.ToHexString(DefaultSC2Key)
                    : Convert.ToHexString(DeviceSetting.DefaultKey);
            };

            void AddDeviceButtonClicked()
            {
                // Validate address
                if (!byte.TryParse(addressTextField.Text.ToString(), out var address) || address > 127)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid address entered!", "OK");
                    return;
                }

                var selectedVersion = scVersionComboBox.SelectedItem == 1
                    ? SecureChannelVersion.V2
                    : SecureChannelVersion.V1;
                var expectedKeyLength = selectedVersion == SecureChannelVersion.V2 ? 64 : 32;

                // Validate key length
                if (keyTextField.Text == null || keyTextField.Text.Length != expectedKeyLength)
                {
                    var expectedBytes = expectedKeyLength / 2;
                    MessageBox.ErrorQuery(40, 10, "Error",
                        $"Key must be {expectedBytes} bytes ({expectedKeyLength} hex chars) for {selectedVersion}!",
                        "OK");
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
                    if (MessageBox.Query(60, 10, "Overwrite",
                            "Device already exists at that address, overwrite?", 1, "No", "Yes") == 0)
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
                result.SecureChannelVersion = selectedVersion;
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

            var dialog = new Dialog("Add Device", 60, 15, cancelButton, addButton);
            dialog.Add(new Label(1, 1, "Name:"), nameTextField,
                new Label(1, 3, "Address:"), addressTextField,
                useCrcCheckBox,
                useSecureChannelCheckBox,
                new Label(1, 8, "SC Version:"), scVersionComboBox,
                new Label(1, 10, "Secure Key:"), keyTextField);
            nameTextField.SetFocus();

            Application.Run(dialog);

            return result;
        }
    }
}