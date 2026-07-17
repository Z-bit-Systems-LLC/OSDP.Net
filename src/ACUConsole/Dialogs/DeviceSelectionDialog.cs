using System.Linq;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="title">Dialog title</param>
        /// <param name="devices">Available devices to choose from</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>DeviceSelectionInput with user's choice</returns>
        public static DeviceSelectionInput Show(IApplication app, string title, DeviceSetting[] devices, string[] deviceList)
        {
            var result = new DeviceSelectionInput { WasCancelled = true };

            var deviceOptionSelector = new OptionSelector
            {
                X = 6,
                Y = 1,
                Width = 50,
                Height = 6,
                Labels = deviceList,
                Value = 0
            };

            void SendCommandButtonClicked()
            {
                var selectedDevice = devices.OrderBy(device => device.Address).ToArray()[deviceOptionSelector.Value ?? 0];
                result.SelectedDeviceAddress = selectedDevice.Address;
                result.WasCancelled = false;
                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                app.RequestStop();
            }

            var sendButton = new Button { Text = "Send", IsDefault = true };
            sendButton.Accepting += (_, e) => { SendCommandButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = title, Width = 60, Height = Dim.Auto() };
            dialog.Add(deviceOptionSelector);
            dialog.AddButton(cancelButton);
            dialog.AddButton(sendButton);
            sendButton.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
