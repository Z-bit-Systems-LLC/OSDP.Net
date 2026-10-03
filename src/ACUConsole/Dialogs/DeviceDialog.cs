using System;
using System.Linq;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting the settings of a device being added or edited
    /// </summary>
    public static class DeviceDialog
    {
        /// <summary>
        /// Shows the add device dialog and returns user input
        /// </summary>
        /// <param name="app">The application instance</param>
        /// <param name="existingDevices">List of existing devices, used to reject an address that is already taken</param>
        /// <returns>DeviceInput with user's choices</returns>
        public static DeviceInput ShowAdd(IApplication app, DeviceSetting[] existingDevices)
        {
            return Show(app, existingDevices, null);
        }

        /// <summary>
        /// Shows the edit device dialog, pre-filled with the device's current settings, and returns user input
        /// </summary>
        /// <param name="app">The application instance</param>
        /// <param name="existingDevices">List of existing devices, used to reject an address that is already taken</param>
        /// <param name="device">The device being edited</param>
        /// <returns>DeviceInput with user's choices</returns>
        public static DeviceInput ShowEdit(IApplication app, DeviceSetting[] existingDevices, DeviceSetting device)
        {
            return Show(app, existingDevices, device ?? throw new ArgumentNullException(nameof(device)));
        }

        private static DeviceInput Show(IApplication app, DeviceSetting[] existingDevices, DeviceSetting deviceToEdit)
        {
            var result = new DeviceInput { WasCancelled = true };
            bool isEdit = deviceToEdit != null;

            var nameTextField = new TextField { X = 15, Y = 1, Width = 35, Text = deviceToEdit?.Name ?? string.Empty };
            var addressTextField = new TextField { X = 15, Y = 3, Width = 35, Text = deviceToEdit?.Address.ToString() ?? string.Empty };
            var useCrcCheckBox = new CheckBox
            {
                X = 1, Y = 5, Text = "Use CRC",
                Value = (deviceToEdit?.UseCrc ?? true) ? CheckState.Checked : CheckState.UnChecked
            };
            var useSecureChannelCheckBox = new CheckBox
            {
                X = 1, Y = 6, Text = "Use Secure Channel",
                Value = (deviceToEdit?.UseSecureChannel ?? true) ? CheckState.Checked : CheckState.UnChecked
            };
            var keyTextField = new TextField
            {
                X = 15, Y = 8, Width = 35,
                Text = Convert.ToHexString(deviceToEdit?.SecureChannelKey ?? DeviceSetting.DefaultKey)
            };

            void PrimaryButtonClicked()
            {
                // Validate address
                if (!byte.TryParse(addressTextField.Text, out var address) || address > 127)
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid address entered!", "OK");
                    return;
                }

                // Validate key length
                if (keyTextField.Text == null || keyTextField.Text.Length != 32)
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid key length entered!", "OK");
                    return;
                }

                // Validate hex key format
                byte[] key;
                try
                {
                    key = Convert.FromHexString(keyTextField.Text!);
                }
                catch
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid hex characters!", "OK");
                    return;
                }

                // Reject an address already used by another device; the device being edited may keep its own
                var existingDevice = existingDevices.FirstOrDefault(d => d.Address == address);
                if (existingDevice != null && existingDevice != deviceToEdit)
                {
                    MessageBox.ErrorQuery(app, "Error",
                        $"Device '{existingDevice.Name}' already exists at address {address}." +
                        (isEdit ? string.Empty : "\nUse Devices > Edit to change it."), "OK");
                    return;
                }

                // All validation passed - collect the data
                result.Name = nameTextField.Text;
                result.Address = address;
                result.UseCrc = useCrcCheckBox.Value == CheckState.Checked;
                result.UseSecureChannel = useSecureChannelCheckBox.Value == CheckState.Checked;
                result.SecureChannelKey = key;
                result.WasCancelled = false;

                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                app.RequestStop();
            }

            var primaryButton = new Button { Text = isEdit ? "Update" : "Add", IsDefault = true };
            primaryButton.Accepting += (_, e) => { PrimaryButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = isEdit ? "Edit Device" : "Add Device", Width = 60, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Name:" }, nameTextField,
                      new Label { X = 1, Y = 3, Text = "Address:" }, addressTextField,
                      useCrcCheckBox,
                      useSecureChannelCheckBox,
                      new Label { X = 1, Y = 8, Text = "Secure Key:" }, keyTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(primaryButton);
            nameTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
