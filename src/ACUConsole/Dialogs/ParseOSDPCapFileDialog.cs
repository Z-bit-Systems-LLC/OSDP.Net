using System;
using System.IO;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui;

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
        /// <param name="initialDirectory">The initial directory to show in the file dialog</param>
        /// <returns>ParseOSDPCapFileInput with user's choices</returns>
        public static ParseOSDPCapFileInput Show(string initialDirectory = "")
        {
            var result = new ParseOSDPCapFileInput { WasCancelled = true };

            // First, show file selection dialog
            var openDialog = new OpenDialog("Load OSDPCap File", initialDirectory ?? string.Empty, new() { ".osdpcap" });
            Application.Run(openDialog);

            if (openDialog.Canceled || !File.Exists(openDialog.FilePath?.ToString()))
            {
                return result;
            }

            var filePath = openDialog.FilePath.ToString();
            
            // Then show parsing options dialog
            var addressTextField = new TextField(30, 1, 20, string.Empty);
            var ignorePollsAndAcksCheckBox = new CheckBox(1, 3, "Ignore Polls And Acks", false);
            var keyTextField = new TextField(15, 5, 35, Convert.ToHexString(DeviceSetting.DefaultKey));

            void ParseButtonClicked()
            {
                byte? address = null;
                if (!string.IsNullOrWhiteSpace(addressTextField.Text.ToString()))
                {
                    if (!byte.TryParse(addressTextField.Text.ToString(), out var addr) || addr > 127)
                    {
                        MessageBox.ErrorQuery(40, 10, "Error", "Invalid address entered!", "OK");
                        return;
                    }
                    address = addr;
                }

                if (keyTextField.Text != null && keyTextField.Text.Length != 32)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid key length entered!", "OK");
                    return;
                }

                byte[] key;
                try
                {
                    key = keyTextField.Text != null ? Convert.FromHexString(keyTextField.Text.ToString()!) : null;
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid hex characters!", "OK");
                    return;
                }

                // All validation passed - collect the data
                result.FilePath = filePath;
                result.FilterAddress = address;
                result.IgnorePollsAndAcks = ignorePollsAndAcksCheckBox.Checked;
                result.SecureKey = key ?? [];
                result.WasCancelled = false;
                Application.RequestStop();
            }

            var parseButton = new Button("Parse", true);
            parseButton.Clicked += ParseButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Parse settings", 60, 13, cancelButton, parseButton);
            dialog.Add(new Label(1, 1, "Filter Specific Address:"), addressTextField,
                      ignorePollsAndAcksCheckBox,
                      new Label(1, 5, "Secure Key:"), keyTextField);
            addressTextField.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}