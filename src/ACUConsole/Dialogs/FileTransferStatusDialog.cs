using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using OSDP.Net;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for showing file transfer progress and status
    /// </summary>
    public static class FileTransferStatusDialog
    {
        /// <summary>
        /// Shows the file transfer status dialog with progress updates
        /// </summary>
        /// <param name="app">Application instance driving the dialog</param>
        /// <param name="onCancel">Action to execute when user cancels</param>
        /// <param name="transferFunc">Function that performs the actual file transfer</param>
        /// <returns>Task that completes when dialog is closed</returns>
        public static async Task Show(IApplication app, Action onCancel, Func<FileTransferStatusDialogHandle, Task> transferFunc)
        {
            var handle = new FileTransferStatusDialogHandle();
            var completionSource = new TaskCompletionSource<bool>();

            var transferStatusLabel = new Label { X = 20, Y = 1, Width = 45, Height = 1, Text = "Initializing..." };
            var progressBar = new ProgressBar { X = 1, Y = 3, Width = 35, Height = 1 };
            var progressPercentage = new Label { X = 40, Y = 3, Width = 10, Height = 1, Text = "0%" };

            var cancelButton = new Button { Text = "Cancel" };
            var dialog = new Dialog { Title = "File Transfer Status", Width = 60, Height = Dim.Auto() };

            cancelButton.Accepting += (_, e) =>
            {
                onCancel?.Invoke();
                app.RequestStop();
                e.Handled = true;
            };

            dialog.Add(new Label { X = 1, Y = 1, Text = "Status:" },
                transferStatusLabel,
                progressBar,
                progressPercentage);
            dialog.AddButton(cancelButton);

            // Set up the handle references
            handle.App = app;
            handle.StatusLabel = transferStatusLabel;
            handle.ProgressBar = progressBar;
            handle.PercentageLabel = progressPercentage;
            handle.CancelButton = cancelButton;
            handle.Dialog = dialog;

            // Start the file transfer in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await transferFunc(handle);

                    // Transfer completed successfully - change Cancel to Close button but don't auto-close
                    app.Invoke(() =>
                    {
                        if (handle.CancelButton != null)
                        {
                            handle.CancelButton.Text = "Close";
                        }
                        completionSource.TrySetResult(true);
                    });
                }
                catch (OperationCanceledException)
                {
                    // Transfer was cancelled
                    app.Invoke(() =>
                    {
                        completionSource.TrySetCanceled();
                        app.RequestStop();
                    });
                }
                catch (Exception ex)
                {
                    // Transfer failed - change Cancel to Close button but don't auto-close
                    app.Invoke(() =>
                    {
                        if (handle.CancelButton != null)
                        {
                            handle.CancelButton.Text = "Close";
                        }
                        completionSource.TrySetException(ex);
                    });
                }
            });

            // Run the dialog modally - this will block until transfer completes or is cancelled
            app.Run(dialog);

            // Wait for completion
            try
            {
                await completionSource.Task;
            }
            catch
            {
                // Exceptions are handled by the caller
            }
            finally
            {
                dialog.Dispose();
            }
        }
    }

    /// <summary>
    /// Handle for updating the file transfer status dialog
    /// </summary>
    public class FileTransferStatusDialogHandle
    {
        internal IApplication App { get; set; }
        internal Label StatusLabel { get; set; }
        internal ProgressBar ProgressBar { get; set; }
        internal Label PercentageLabel { get; set; }
        internal Button CancelButton { get; set; }
        internal Dialog Dialog { get; set; }

        /// <summary>
        /// Splits camelCase enum values into readable text with spaces
        /// </summary>
        private static string SplitCamelCase(string str)
        {
            return Regex.Replace(
                Regex.Replace(
                    str,
                    @"(\P{Ll})(\P{Ll}\p{Ll})",
                    "$1 $2"
                ),
                @"(\p{Ll})(\P{Ll})",
                "$1 $2"
            );
        }

        /// <summary>
        /// Updates the progress display
        /// </summary>
        /// <param name="status">Current file transfer status</param>
        /// <param name="totalSize">Total file size in bytes</param>
        public void UpdateProgress(ControlPanel.FileTransferStatus status, int totalSize)
        {
            App.Invoke(() =>
            {
                if (StatusLabel != null)
                {
                    StatusLabel.Text = status?.Status != null ? SplitCamelCase(status.Status.ToString()) : "Unknown";
                }

                if (ProgressBar != null && PercentageLabel != null && totalSize > 0)
                {
                    if (status?.CurrentOffset != null)
                    {
                        float percentage = (float)status.CurrentOffset / totalSize;
                        ProgressBar.Fraction = percentage;
                        PercentageLabel.Text = percentage.ToString("P");
                    }
                }

                // Change the Cancel button to Close when the transfer is complete or failed
                if (CancelButton != null && status != null)
                {
                    if (status.Status != OSDP.Net.Model.ReplyData.FileTransferStatus.StatusDetail.OkToProceed &&
                        status.Status != OSDP.Net.Model.ReplyData.FileTransferStatus.StatusDetail.FinishingFileTransfer)
                    {
                        CancelButton.Text = "Close";
                    }
                }
            });
        }

        /// <summary>
        /// Closes the dialog
        /// </summary>
        public void Close()
        {
            App.Invoke(() =>
            {
                if (Dialog != null)
                {
                    App.RequestStop(Dialog);
                }
            });
        }
    }
}
