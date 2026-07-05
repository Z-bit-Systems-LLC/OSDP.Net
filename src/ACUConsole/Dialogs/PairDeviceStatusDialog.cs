using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OSDP.Net.Pairing;
using Terminal.Gui;

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
        /// <param name="pairFunc">The pairing operation, given a handle to report progress.</param>
        public static async Task Show(Func<PairDeviceStatusDialogHandle, Task> pairFunc)
        {
            var handle = new PairDeviceStatusDialogHandle();
            var completionSource = new TaskCompletionSource<bool>();

            var stageLabel = new Label(new Rect(20, 1, 38, 1), "Starting...");
            var progressBar = new ProgressBar(new Rect(1, 3, 45, 1));
            var progressPercentage = new Label(new Rect(48, 3, 8, 1), "0%");

            var closeButton = new Button("Cancel");
            var dialog = new Dialog("Asymmetric Pairing", 62, 10, closeButton);

            closeButton.Clicked += () => Application.RequestStop(dialog);

            dialog.Add(new Label(1, 1, "Stage:"), stageLabel, progressBar, progressPercentage);

            handle.StageLabel = stageLabel;
            handle.ProgressBar = progressBar;
            handle.PercentageLabel = progressPercentage;
            handle.CloseButton = closeButton;

            _ = Task.Run(async () =>
            {
                try
                {
                    await pairFunc(handle);
                    Application.MainLoop.Invoke(() =>
                    {
                        handle.ShowResult("Paired — SC2 secure channel establishing", 1.0);
                        closeButton.Text = "Close";
                        completionSource.TrySetResult(true);
                    });
                }
                catch (Exception ex)
                {
                    Application.MainLoop.Invoke(() =>
                    {
                        handle.ShowError(ex.Message);
                        closeButton.Text = "Close";
                        completionSource.TrySetException(ex);
                    });
                }
            });

            Application.Run(dialog);

            try
            {
                await completionSource.Task;
            }
            catch
            {
                // Surfaced to the caller via the dialog; swallow here.
            }
        }
    }

    /// <summary>
    /// Handle for updating the pairing status dialog.
    /// </summary>
    public class PairDeviceStatusDialogHandle
    {
        internal Label StageLabel { get; set; }
        internal ProgressBar ProgressBar { get; set; }
        internal Label PercentageLabel { get; set; }
        internal Button CloseButton { get; set; }

        /// <summary>Updates the progress bar and stage label from a pairing progress report.</summary>
        public void Report(PairingProgress progress)
        {
            Application.MainLoop.Invoke(() =>
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
