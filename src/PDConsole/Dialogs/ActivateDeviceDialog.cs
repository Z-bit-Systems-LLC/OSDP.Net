using System;
using System.IO.Ports;
using System.Linq;
using OSDP.Net.Connections;
using OSDP.Net.Messages.SecureChannel;
using PDConsole.Configuration;
using PDConsole.Extensions;
using PDConsole.Model.DialogInputs;
using Terminal.Gui;

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
        /// <param name="connection">Current connection settings for defaults.</param>
        /// <param name="security">Current security settings for defaults.</param>
        public static ActivateDeviceInput Show(ConnectionSettings connection, SecuritySettings security)
        {
            var result = new ActivateDeviceInput { WasCancelled = true };

            var portComboBox = CreatePortComboBox(15, 1, connection.SerialPortName).ConfigureForOptimalUX();
            var baudComboBox = CreateBaudComboBox(15, 3, connection.SerialBaudRate).ConfigureForOptimalUX();
            var modeComboBox = new ComboBox(new Rect(15, 5, 30, 5), SecurityModes).ConfigureForOptimalUX();
            var versionComboBox = new ComboBox(new Rect(15, 7, 30, 5), SecurityVersions).ConfigureForOptimalUX();
            var keyField = new TextField(15, 9, 48, security.SecureChannelKey);
            var demoCaCheckBox = new CheckBox(1, 11, "Use demonstration CA (pairing)", security.Pairing.UseDemoCa);
            var seedField = new TextField(15, 13, 48, security.Pairing.DeviceSeedHex);

            modeComboBox.SelectedItem = (int)security.SecureChannelMode;
            versionComboBox.SelectedItem = security.SecureChannelVersion == SecureChannelVersion.V2 ? 1 : 0;

            // Enable only the fields relevant to the selected mode.
            void UpdateEnabledState()
            {
                var mode = (SecureChannelMode)modeComboBox.SelectedItem;

                // Pairing always targets SC2, so pin the version to V2 and lock it.
                if (mode == SecureChannelMode.Pairing)
                {
                    versionComboBox.SelectedItem = 1;
                }

                versionComboBox.Enabled = mode is SecureChannelMode.Install or SecureChannelMode.Secure;
                keyField.Enabled = mode == SecureChannelMode.Secure;
                demoCaCheckBox.Enabled = mode == SecureChannelMode.Pairing;
                seedField.Enabled = mode == SecureChannelMode.Pairing;
            }

            modeComboBox.SelectedItemChanged += _ => UpdateEnabledState();
            UpdateEnabledState();

            void StartClicked()
            {
                var portName = portComboBox.Text.ToString();
                if (string.IsNullOrEmpty(portName) || portName == "No ports available")
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "No port name selected!", "OK");
                    return;
                }

                if (!int.TryParse(baudComboBox.Text.ToString(), out var baudRate))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid baud rate selected!", "OK");
                    return;
                }

                var mode = (SecureChannelMode)modeComboBox.SelectedItem;
                var version = versionComboBox.SelectedItem == 1 ? SecureChannelVersion.V2 : SecureChannelVersion.V1;
                var key = keyField.Text.ToString() ?? string.Empty;
                var seed = seedField.Text.ToString() ?? string.Empty;

                if (mode == SecureChannelMode.Secure && !TryValidateKey(key, version))
                {
                    return;
                }

                if (mode == SecureChannelMode.Pairing && !TryValidateSeed(seed))
                {
                    return;
                }

                result.PortName = portName;
                result.BaudRate = baudRate;
                result.SecureChannelMode = mode;
                result.SecureChannelVersion = version;
                result.SecureChannelKey = key;
                result.UseDemoCa = demoCaCheckBox.Checked;
                result.DeviceSeedHex = seed;
                result.WasCancelled = false;

                Application.RequestStop();
            }

            void CancelClicked()
            {
                result.WasCancelled = true;
                Application.RequestStop();
            }

            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelClicked;
            var startButton = new Button("Start", true);
            startButton.Clicked += StartClicked;

            var dialog = new Dialog("Activate Device", 66, 18, cancelButton, startButton);
            dialog.Add(
                new Label(1, 1, "Port:"), portComboBox,
                new Label(1, 3, "Baud Rate:"), baudComboBox,
                new Label(1, 5, "Security:"), modeComboBox,
                new Label(1, 7, "Version:"), versionComboBox,
                new Label(1, 9, "SC Key:"), keyField,
                demoCaCheckBox,
                new Label(1, 13, "Seed:"), seedField);
            portComboBox.SetFocus();

            Application.Run(dialog);

            return result;
        }

        private static bool TryValidateKey(string hexKey, SecureChannelVersion version)
        {
            var cleaned = (hexKey ?? string.Empty).Replace(" ", "").Replace("-", "");
            byte[] key;
            try
            {
                key = Convert.FromHexString(cleaned);
            }
            catch (FormatException)
            {
                MessageBox.ErrorQuery(50, 10, "Error", "Secure channel key must be a valid hex string.", "OK");
                return false;
            }

            var expected = version == SecureChannelVersion.V2 ? 32 : 16;
            if (key.Length != expected)
            {
                MessageBox.ErrorQuery(50, 10, "Error",
                    $"Secure channel key must be {expected} bytes ({expected * 2} hex characters).", "OK");
                return false;
            }

            return true;
        }

        private static bool TryValidateSeed(string seedHex)
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
                MessageBox.ErrorQuery(50, 10, "Error", "Device seed must be a valid hex string.", "OK");
                return false;
            }

            if (seed.Length != 32)
            {
                MessageBox.ErrorQuery(50, 10, "Error",
                    "Device seed must be 32 bytes (64 hex characters), or empty for a random key.", "OK");
                return false;
            }

            return true;
        }

        private static ComboBox CreatePortComboBox(int x, int y, string currentPortName)
        {
            var portNames = SerialPort.GetPortNames();
            if (portNames.Length == 0)
            {
                portNames = ["No ports available"];
            }

            var portComboBox = new ComboBox(new Rect(x, y, 30, 5), portNames);
            if (!portNames[0].Equals("No ports available"))
            {
                var index = Array.FindIndex(portNames, port =>
                    string.Equals(port, currentPortName, StringComparison.OrdinalIgnoreCase));
                portComboBox.SelectedItem = Math.Max(index, 0);
            }

            return portComboBox;
        }

        private static ComboBox CreateBaudComboBox(int x, int y, int currentBaudRate)
        {
            var baudComboBox = new ComboBox(new Rect(x, y, 30, 5), StandardBaudRates);
            var index = Array.FindIndex(StandardBaudRates, rate => rate == currentBaudRate.ToString());
            baudComboBox.SelectedItem = Math.Max(index, 0);
            return baudComboBox;
        }
    }
}
