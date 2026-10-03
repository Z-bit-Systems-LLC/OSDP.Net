using System.Linq;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="devices">List of available devices.</param>
        /// <param name="deviceList">Formatted device list for display.</param>
        /// <returns>A <see cref="PairDeviceInput"/> with the user's choice.</returns>
        public static PairDeviceInput Show(IApplication app, DeviceSetting[] devices, string[] deviceList)
        {
            var result = new PairDeviceInput { WasCancelled = true };

            if (deviceList.Length == 0)
            {
                MessageBox.ErrorQuery(app, "Pair Device",
                    "No devices are configured. Add a device before pairing.", "OK");
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

            void PairButtonClicked()
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

            var pairButton = new Button { Text = "Pair", IsDefault = true };
            pairButton.Accepting += (_, e) => { PairButtonClicked(); e.Handled = true; };
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };

            var dialog = new Dialog { Title = "Pair Device (Asymmetric)", Width = 60, Height = Dim.Auto() };
            dialog.Add(deviceOptionSelector);
            dialog.AddButton(cancelButton);
            dialog.AddButton(pairButton);
            pairButton.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
