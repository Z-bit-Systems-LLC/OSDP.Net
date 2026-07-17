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
    /// Dialog for collecting device addition parameters
    /// </summary>
    public static class AddDeviceDialog
    {
        /// <summary>
        /// Shows the add device dialog and returns user input
        /// </summary>
        /// <param name="app">The application instance</param>
        /// <param name="existingDevices">List of existing devices to check for duplicates</param>
        /// <returns>AddDeviceInput with user's choices</returns>
        public static AddDeviceInput Show(IApplication app, DeviceSetting[] existingDevices)
        {
            var result = new AddDeviceInput { WasCancelled = true };

            var nameTextField = new TextField { X = 15, Y = 1, Width = 35, Text = string.Empty };
            var addressTextField = new TextField { X = 15, Y = 3, Width = 35, Text = string.Empty };
            var useCrcCheckBox = new CheckBox { X = 1, Y = 5, Text = "Use CRC", Value = CheckState.Checked };
            var useSecureChannelCheckBox = new CheckBox { X = 1, Y = 6, Text = "Use Secure Channel", Value = CheckState.Checked };
            var keyTextField = new TextField { X = 15, Y = 8, Width = 35, Text = Convert.ToHexString(DeviceSetting.DefaultKey) };

            void AddDeviceButtonClicked()
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

                // Check for existing device at address
                var existingDevice = existingDevices.FirstOrDefault(d => d.Address == address);
                bool overwriteExisting = false;
                if (existingDevice != null)
                {
                    if (MessageBox.Query(app, 60, 10, "Overwrite", "Device already exists at that address, overwrite?", "No", "Yes") == 0)
                    {
                        return;
                    }
                    overwriteExisting = true;
                }

                // All validation passed - collect the data
                result.Name = nameTextField.Text;
                result.Address = address;
                result.UseCrc = useCrcCheckBox.Value == CheckState.Checked;
                result.UseSecureChannel = useSecureChannelCheckBox.Value == CheckState.Checked;
                result.SecureChannelKey = key;
                result.OverwriteExisting = overwriteExisting;
                result.WasCancelled = false;

                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                app.RequestStop();
            }

            var addButton = new Button { Text = "Add", IsDefault = true };
            addButton.Accepting += (_, e) => { AddDeviceButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = "Add Device", Width = 60, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Name:" }, nameTextField,
                      new Label { X = 1, Y = 3, Text = "Address:" }, addressTextField,
                      useCrcCheckBox,
                      useSecureChannelCheckBox,
                      new Label { X = 1, Y = 8, Text = "Secure Key:" }, keyTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(addButton);
            nameTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
