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
    /// Dialog for parsing OSDP Cap files with filtering options
    /// </summary>
    public static class ParseOSDPCapFileDialog
    {
        /// <summary>
        /// Shows the parse OSDP cap file dialog and returns user input
        /// </summary>
        /// <param name="app">The application instance</param>
        /// <param name="initialDirectory">The initial directory to show in the file dialog</param>
        /// <returns>ParseOSDPCapFileInput with user's choices</returns>
        public static ParseOSDPCapFileInput Show(IApplication app, string initialDirectory = "")
        {
            var result = new ParseOSDPCapFileInput { WasCancelled = true };

            // First, show file selection dialog
            var openDialog = new OpenDialog
            {
                Title = "Load OSDPCap File",
                Path = initialDirectory ?? string.Empty,
                AllowedTypes = { new AllowedType("OSDP Capture", ".osdpcap") }
            };
            app.Run(openDialog);

            if (openDialog.Canceled || !File.Exists(openDialog.Path))
            {
                openDialog.Dispose();
                return result;
            }

            var filePath = openDialog.Path;
            openDialog.Dispose();

            // Then show parsing options dialog
            var addressTextField = new TextField { X = 30, Y = 1, Width = 20, Text = string.Empty };
            var ignorePollsAndAcksCheckBox = new CheckBox { X = 1, Y = 3, Text = "Ignore Polls And Acks", Value = CheckState.UnChecked };
            var keyTextField = new TextField { X = 15, Y = 5, Width = 35, Text = Convert.ToHexString(DeviceSetting.DefaultKey) };

            void ParseButtonClicked()
            {
                byte? address = null;
                if (!string.IsNullOrWhiteSpace(addressTextField.Text))
                {
                    if (!byte.TryParse(addressTextField.Text, out var addr) || addr > 127)
                    {
                        MessageBox.ErrorQuery(app, "Error", "Invalid address entered!", "OK");
                        return;
                    }
                    address = addr;
                }

                if (keyTextField.Text != null && keyTextField.Text.Length != 32)
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid key length entered!", "OK");
                    return;
                }

                byte[] key;
                try
                {
                    key = keyTextField.Text != null ? Convert.FromHexString(keyTextField.Text!) : null;
                }
                catch
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid hex characters!", "OK");
                    return;
                }

                // All validation passed - collect the data
                result.FilePath = filePath;
                result.FilterAddress = address;
                result.IgnorePollsAndAcks = ignorePollsAndAcksCheckBox.Value == CheckState.Checked;
                result.SecureKey = key ?? [];
                result.WasCancelled = false;
                app.RequestStop();
            }

            var parseButton = new Button { Text = "Parse", IsDefault = true };
            parseButton.Accepting += (_, e) => { ParseButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { app.RequestStop(); e.Handled = true; };

            var dialog = new Dialog { Title = "Parse settings", Width = 60, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Filter Specific Address:" }, addressTextField,
                      ignorePollsAndAcksCheckBox,
                      new Label { X = 1, Y = 5, Text = "Secure Key:" }, keyTextField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(parseButton);
            addressTextField.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
