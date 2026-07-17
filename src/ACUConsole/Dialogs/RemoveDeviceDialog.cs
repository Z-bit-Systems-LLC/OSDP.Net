using System.Linq;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="devices">List of available devices to remove</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>RemoveDeviceInput with user's choice</returns>
        public static RemoveDeviceInput Show(IApplication app, DeviceSetting[] devices, string[] deviceList)
        {
            var result = new RemoveDeviceInput { WasCancelled = true };

            if (deviceList.Length == 0)
            {
                MessageBox.ErrorQuery(app, "Information", "No devices to remove.", "OK");
                return result;
            }

            var deviceOptionSelector = new OptionSelector
            {
                X = 6,
                Y = 1,
                Width = 50,
                Height = 6,
                Labels = deviceList,
                Value = 0
            };

            void RemoveDeviceButtonClicked()
            {
                var selectedDevice = devices.OrderBy(d => d.Address).ToArray()[deviceOptionSelector.Value ?? 0];
                result.DeviceAddress = selectedDevice.Address;
                result.WasCancelled = false;
                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                app.RequestStop();
            }

            var removeButton = new Button { Text = "Remove", IsDefault = true };
            removeButton.Accepting += (_, e) => { RemoveDeviceButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = "Remove Device", Width = 60, Height = Dim.Auto() };
            dialog.Add(deviceOptionSelector);
            dialog.AddButton(cancelButton);
            dialog.AddButton(removeButton);
            removeButton.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
