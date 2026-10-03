using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using OSDP.Net.Connections;
using OSDP.Net.Messages.SecureChannel;
using PDConsole.Configuration;
using PDConsole.Extensions;
using PDConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace PDConsole.Dialogs
{
    /// <summary>
    /// Dialog for activating the PD: selects the serial connection and the full set of secure channel
    /// options (Clear Text, Install / SCBK-D, Secure / SCBK, and asymmetric Pairing). The secure fields
    /// enable and disable as the selected mode requires.
    /// </summary>
    public static class ActivateDeviceDialog
    {
        // Index order matches the SecureChannelMode enum (ClearText, Install, Secure, Pairing).
        private static readonly string[] SecurityModes =
            ["Clear Text", "Install (SCBK-D)", "Secure (SCBK)", "Pairing (asymmetric)"];

        // Index order matches SecureChannelVersion (V1, V2).
        private static readonly string[] SecurityVersions = ["V1 (AES-128)", "V2 (AES-256)"];

        private static readonly string[] StandardBaudRates =
            SerialPortOsdpConnection.StandardBaudRates.Select(r => r.ToString()).ToArray();

        /// <summary>
        /// Shows the activate-device dialog and returns the user's connection and security choices.
        /// </summary>
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="connection">Current connection settings for defaults.</param>
        /// <param name="security">Current security settings for defaults.</param>
        public static ActivateDeviceInput Show(IApplication app, ConnectionSettings connection, SecuritySettings security)
        {
            var result = new ActivateDeviceInput { WasCancelled = true };

            var portComboBox = CreatePortComboBox(15, 1, connection.SerialPortName).ConfigureForOptimalUX();
            var baudComboBox = CreateBaudComboBox(15, 3, connection.SerialBaudRate).ConfigureForOptimalUX();
            var modeComboBox = CreateDropDownList(15, 5, SecurityModes).ConfigureForOptimalUX();
            var versionComboBox = CreateDropDownList(15, 7, SecurityVersions).ConfigureForOptimalUX();
            var keyField = new TextField { X = 15, Y = 9, Width = 48, Text = security.SecureChannelKey };
            var demoCaCheckBox = new CheckBox
            {
                X = 1,
                Y = 11,
                Text = "Use demonstration CA (pairing)",
                Value = security.Pairing.UseDemoCa ? CheckState.Checked : CheckState.UnChecked
            };
            var seedField = new TextField { X = 15, Y = 13, Width = 48, Text = security.Pairing.DeviceSeedHex };

            modeComboBox.Text = SecurityModes[(int)security.SecureChannelMode];
            versionComboBox.Text = SecurityVersions[security.SecureChannelVersion == SecureChannelVersion.V2 ? 1 : 0];

            // The drop-down lists are editable, so text that matches no entry maps to -1.
            int SelectedModeIndex() => Array.IndexOf(SecurityModes, modeComboBox.Text);

            SecureChannelVersion SelectedVersion() =>
                versionComboBox.Text == SecurityVersions[1] ? SecureChannelVersion.V2 : SecureChannelVersion.V1;

            // Enable only the fields relevant to the selected mode.
            void UpdateEnabledState()
            {
                var mode = (SecureChannelMode)Math.Max(SelectedModeIndex(), 0);

                // Pairing always targets SC2, so pin the version to V2 and lock it.
                if (mode == SecureChannelMode.Pairing)
                {
                    versionComboBox.Text = SecurityVersions[1];
                }

                versionComboBox.Enabled = mode is SecureChannelMode.Install or SecureChannelMode.Secure;
                keyField.Enabled = mode == SecureChannelMode.Secure;
                demoCaCheckBox.Enabled = mode == SecureChannelMode.Pairing;
                seedField.Enabled = mode == SecureChannelMode.Pairing;
            }

            modeComboBox.TextChanged += (_, _) => UpdateEnabledState();
            UpdateEnabledState();

            void StartClicked()
            {
                var portName = portComboBox.Text;
                if (string.IsNullOrEmpty(portName) || portName == "No ports available")
                {
                    MessageBox.ErrorQuery(app, "Error", "No port name selected!", "OK");
                    return;
                }

                if (!int.TryParse(baudComboBox.Text, out var baudRate))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid baud rate selected!", "OK");
                    return;
                }

                var modeIndex = SelectedModeIndex();
                if (modeIndex < 0)
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid security mode selected!", "OK");
                    return;
                }

                var mode = (SecureChannelMode)modeIndex;
                var version = SelectedVersion();
                var key = keyField.Text ?? string.Empty;
                var seed = seedField.Text ?? string.Empty;

                if (mode == SecureChannelMode.Secure && !TryValidateKey(app, key, version))
                {
                    return;
                }

                if (mode == SecureChannelMode.Pairing && !TryValidateSeed(app, seed))
                {
                    return;
                }

                result.PortName = portName;
                result.BaudRate = baudRate;
                result.SecureChannelMode = mode;
                result.SecureChannelVersion = version;
                result.SecureChannelKey = key;
                result.UseDemoCa = demoCaCheckBox.Value == CheckState.Checked;
                result.DeviceSeedHex = seed;
                result.WasCancelled = false;

                app.RequestStop();
            }

            void CancelClicked()
            {
                result.WasCancelled = true;
                app.RequestStop();
            }

            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelClicked(); e.Handled = true; };
            var startButton = new Button { Text = "Start", IsDefault = true };
            startButton.Accepting += (_, e) => { StartClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = "Activate Device", Width = 66, Height = Dim.Auto() };
            dialog.Add(
                new Label { X = 1, Y = 1, Text = "Port:" }, portComboBox,
                new Label { X = 1, Y = 3, Text = "Baud Rate:" }, baudComboBox,
                new Label { X = 1, Y = 5, Text = "Security:" }, modeComboBox,
                new Label { X = 1, Y = 7, Text = "Version:" }, versionComboBox,
                new Label { X = 1, Y = 9, Text = "SC Key:" }, keyField,
                demoCaCheckBox,
                new Label { X = 1, Y = 13, Text = "Seed:" }, seedField);
            dialog.AddButton(cancelButton);
            dialog.AddButton(startButton);
            portComboBox.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }

        private static bool TryValidateKey(IApplication app, string hexKey, SecureChannelVersion version)
        {
            var cleaned = (hexKey ?? string.Empty).Replace(" ", "").Replace("-", "");
            byte[] key;
            try
            {
                key = Convert.FromHexString(cleaned);
            }
            catch (FormatException)
            {
                MessageBox.ErrorQuery(app, "Error", "Secure channel key must be a valid hex string.", "OK");
                return false;
            }

            var expected = version == SecureChannelVersion.V2 ? 32 : 16;
            if (key.Length != expected)
            {
                MessageBox.ErrorQuery(app, "Error",
                    $"Secure channel key must be {expected} bytes ({expected * 2} hex characters).", "OK");
                return false;
            }

            return true;
        }

        private static bool TryValidateSeed(IApplication app, string seedHex)
        {
            var cleaned = (seedHex ?? string.Empty).Replace(" ", "").Replace("-", "");
            if (cleaned.Length == 0)
            {
                return true; // Empty seed = randomly generated device key.
            }

            byte[] seed;
            try
            {
                seed = Convert.FromHexString(cleaned);
            }
            catch (FormatException)
            {
                MessageBox.ErrorQuery(app, "Error", "Device seed must be a valid hex string.", "OK");
                return false;
            }

            if (seed.Length != 32)
            {
                MessageBox.ErrorQuery(app, "Error",
                    "Device seed must be 32 bytes (64 hex characters), or empty for a random key.", "OK");
                return false;
            }

            return true;
        }

        private static DropDownList CreateDropDownList(int x, int y, string[] items) =>
            new()
            {
                X = x,
                Y = y,
                Width = 30,
                Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(items))
            };

        private static DropDownList CreatePortComboBox(int x, int y, string currentPortName)
        {
            var portNames = SerialPort.GetPortNames();
            if (portNames.Length == 0)
            {
                portNames = ["No ports available"];
            }

            var portComboBox = CreateDropDownList(x, y, portNames);
            if (!portNames[0].Equals("No ports available"))
            {
                var index = Array.FindIndex(portNames, port =>
                    string.Equals(port, currentPortName, StringComparison.OrdinalIgnoreCase));
                portComboBox.Text = portNames[Math.Max(index, 0)];
            }

            return portComboBox;
        }

        private static DropDownList CreateBaudComboBox(int x, int y, int currentBaudRate)
        {
            var baudComboBox = CreateDropDownList(x, y, StandardBaudRates);
            var index = Array.FindIndex(StandardBaudRates, rate => rate == currentBaudRate.ToString());
            baudComboBox.Text = StandardBaudRates[Math.Max(index, 0)];
            return baudComboBox;
        }
    }
}
