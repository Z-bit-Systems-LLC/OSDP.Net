using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OSDP.Net.Pairing;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog showing asymmetric pairing progress with a progress bar, modeled on the file-transfer
    /// status dialog.
    /// </summary>
    public static class PairDeviceStatusDialog
    {
        /// <summary>
        /// Shows the pairing status dialog and runs the pairing operation, driving a progress bar.
        /// </summary>
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="pairFunc">The pairing operation, given a handle to report progress.</param>
        public static async Task Show(IApplication app, Func<PairDeviceStatusDialogHandle, Task> pairFunc)
        {
            var handle = new PairDeviceStatusDialogHandle();
            var completionSource = new TaskCompletionSource<bool>();

            var stageLabel = new Label { X = 20, Y = 1, Width = 38, Height = 1, Text = "Starting..." };
            var progressBar = new ProgressBar { X = 1, Y = 3, Width = 45, Height = 1 };
            var progressPercentage = new Label { X = 48, Y = 3, Width = 8, Height = 1, Text = "0%" };

            var closeButton = new Button { Text = "Cancel" };
            var dialog = new Dialog { Title = "Asymmetric Pairing", Width = 62, Height = Dim.Auto() };

            closeButton.Accepting += (_, e) =>
            {
                app.RequestStop();
                e.Handled = true;
            };

            dialog.Add(new Label { X = 1, Y = 1, Text = "Stage:" }, stageLabel, progressBar, progressPercentage);
            dialog.AddButton(closeButton);

            handle.App = app;
            handle.StageLabel = stageLabel;
            handle.ProgressBar = progressBar;
            handle.PercentageLabel = progressPercentage;

            _ = Task.Run(async () =>
            {
                try
                {
                    await pairFunc(handle);
                    app.Invoke(() =>
                    {
                        handle.ShowResult("Paired — SC2 secure channel establishing", 1.0);
                        closeButton.Text = "Close";
                        completionSource.TrySetResult(true);
                    });
                }
                catch (Exception ex)
                {
                    app.Invoke(() =>
                    {
                        handle.ShowError(ex.Message);
                        closeButton.Text = "Close";
                        completionSource.TrySetException(ex);
                    });
                }
            });

            app.Run(dialog);

            try
            {
                await completionSource.Task;
            }
            catch
            {
                // Surfaced to the caller via the dialog; swallow here.
            }
            finally
            {
                dialog.Dispose();
            }
        }
    }

    /// <summary>
    /// Handle for updating the pairing status dialog.
    /// </summary>
    public class PairDeviceStatusDialogHandle
    {
        internal IApplication App { get; set; }
        internal Label StageLabel { get; set; }
        internal ProgressBar ProgressBar { get; set; }
        internal Label PercentageLabel { get; set; }

        /// <summary>Updates the progress bar and stage label from a pairing progress report.</summary>
        public void Report(PairingProgress progress)
        {
            App.Invoke(() =>
            {
                if (StageLabel != null)
                {
                    StageLabel.Text = SplitCamelCase(progress.Stage.ToString());
                }

                if (ProgressBar != null)
                {
                    ProgressBar.Fraction = (float)progress.Fraction;
                }

                if (PercentageLabel != null)
                {
                    PercentageLabel.Text = progress.Fraction.ToString("P0");
                }
            });
        }

        internal void ShowResult(string message, double fraction)
        {
            if (StageLabel != null) StageLabel.Text = message;
            if (ProgressBar != null) ProgressBar.Fraction = (float)fraction;
            if (PercentageLabel != null) PercentageLabel.Text = fraction.ToString("P0");
        }

        internal void ShowError(string message)
        {
            if (StageLabel != null) StageLabel.Text = "Failed: " + message;
        }

        private static string SplitCamelCase(string value) =>
            Regex.Replace(value, "(\\B[A-Z])", " $1");
    }
}
