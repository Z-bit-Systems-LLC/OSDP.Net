using System;
using System.Linq;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using NStack;
using Terminal.Gui;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for selecting a device to remove
    /// </summary>
    public static class RemoveDeviceDialog
    {
        /// <summary>
        /// Shows the remove device dialog and returns user selection
        /// </summary>
        /// <param name="devices">List of available devices to remove</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>RemoveDeviceInput with user's choice</returns>
        public static RemoveDeviceInput Show(DeviceSetting[] devices, string[] deviceList)
        {
            var result = new RemoveDeviceInput { WasCancelled = true };

            if (deviceList.Length == 0)
            {
                MessageBox.ErrorQuery(60, 10, "Information", "No devices to remove.", "OK");
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

            void RemoveDeviceButtonClicked()
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

            var removeButton = new Button("Remove", true);
            removeButton.Clicked += RemoveDeviceButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("Remove Device", 60, 13, cancelButton, removeButton);
            dialog.Add(scrollView);
            removeButton.SetFocus();

            Application.Run(dialog);
            
            return result;
        }
    }
}