using System.Linq;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using NStack;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for selecting a device from available devices
    /// </summary>
    public static class DeviceSelectionDialog
    {
        /// <summary>
        /// Shows the device selection dialog and returns user selection
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="devices">Available devices to choose from</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>DeviceSelectionInput with user's choice</returns>
        public static DeviceSelectionInput Show(string title, DeviceSetting[] devices, string[] deviceList)
        {
            var result = new DeviceSelectionInput { WasCancelled = true };

            var scrollView = new ScrollView(new Rect(6, 1, 50, 6))
            {
                ContentSize = new Size(40, deviceList.Length * 2),
                ShowVerticalScrollIndicator = deviceList.Length > 6,
                ShowHorizontalScrollIndicator = false
            };

            var deviceRadioGroup = new RadioGroup(0, 0, deviceList.Select(ustring.Make).ToArray())
            {
                SelectedItem = 0
            };
            scrollView.Add(deviceRadioGroup);

            void SendCommandButtonClicked()
            {
                var selectedDevice = devices.OrderBy(device => device.Address).ToArray()[deviceRadioGroup.SelectedItem];
                result.SelectedDeviceAddress = selectedDevice.Address;
                result.WasCancelled = false;
                Application.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                Application.RequestStop();
            }

            var sendButton = new Button("Send", true);
            sendButton.Clicked += SendCommandButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog(title, 60, 13, cancelButton, sendButton);
            dialog.Add(scrollView);
            sendButton.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}