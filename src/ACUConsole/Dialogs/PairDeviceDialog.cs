using System.Linq;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using NStack;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for selecting a device to pair with using asymmetric (EDHOC-style) pairing.
    /// </summary>
    public static class PairDeviceDialog
    {
        /// <summary>
        /// Shows the pair device dialog and returns the user's selection.
        /// </summary>
        /// <param name="devices">List of available devices.</param>
        /// <param name="deviceList">Formatted device list for display.</param>
        /// <returns>A <see cref="PairDeviceInput"/> with the user's choice.</returns>
        public static PairDeviceInput Show(DeviceSetting[] devices, string[] deviceList)
        {
            var result = new PairDeviceInput { WasCancelled = true };

            if (deviceList.Length == 0)
            {
                MessageBox.ErrorQuery(60, 10, "Pair Device",
                    "No devices are configured. Add a device before pairing.", "OK");
                return result;
            }

            var scrollView = new ScrollView(new Rect(6, 1, 50, 6))
            {
                ContentSize = new Size(40, deviceList.Length * 2),
                ShowVerticalScrollIndicator = deviceList.Length > 6,
                ShowHorizontalScrollIndicator = false
            };

            var deviceRadioGroup = new RadioGroup(0, 0, deviceList.Select(ustring.Make).ToArray());
            scrollView.Add(deviceRadioGroup);

            void PairButtonClicked()
            {
                var selectedDevice = devices.OrderBy(d => d.Address).ToArray()[deviceRadioGroup.SelectedItem];
                result.DeviceAddress = selectedDevice.Address;
                result.WasCancelled = false;
                Application.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                Application.RequestStop();
            }

            var pairButton = new Button("Pair", true);
            pairButton.Clicked += PairButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("Pair Device (Asymmetric)", 60, 13, cancelButton, pairButton);
            dialog.Add(scrollView);
            pairButton.SetFocus();

            Application.Run(dialog);

            return result;
        }
    }
}
