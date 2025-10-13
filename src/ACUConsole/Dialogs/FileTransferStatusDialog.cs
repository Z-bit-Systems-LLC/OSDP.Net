using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Terminal.Gui;
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
        /// <param name="onCancel">Action to execute when user cancels</param>
        /// <param name="transferFunc">Function that performs the actual file transfer</param>
        /// <returns>Task that completes when dialog is closed</returns>
        public static async Task Show(Action onCancel, Func<FileTransferStatusDialogHandle, Task> transferFunc)
        {
            var handle = new FileTransferStatusDialogHandle();
            var completionSource = new TaskCompletionSource<bool>();

            var transferStatusLabel = new Label(new Rect(20, 1, 45, 1), "Initializing...");
            var progressBar = new ProgressBar(new Rect(1, 3, 35, 1));
            var progressPercentage = new Label(new Rect(40, 3, 10, 1), "0%");

            var cancelButton = new Button("Cancel");
            var dialog = new Dialog("File Transfer Status", 60, 10, cancelButton);

            cancelButton.Clicked += () =>
            {
                onCancel?.Invoke();
                Application.RequestStop(dialog);
            };

            dialog.Add(new Label(1, 1, "Status:"),
                transferStatusLabel,
                progressBar,
                progressPercentage);

            // Set up the handle references
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

                    // Transfer completed successfully
                    Application.MainLoop.Invoke(() =>
                    {
                        if (handle.CancelButton != null)
                        {
                            handle.CancelButton.Text = "Close";
                        }
                        completionSource.TrySetResult(true);
                        Application.RequestStop(dialog);
                    });
                }
                catch (OperationCanceledException)
                {
                    // Transfer was cancelled
                    Application.MainLoop.Invoke(() =>
                    {
                        completionSource.TrySetCanceled();
                        Application.RequestStop(dialog);
                    });
                }
                catch (Exception ex)
                {
                    // Transfer failed
                    Application.MainLoop.Invoke(() =>
                    {
                        if (handle.CancelButton != null)
                        {
                            handle.CancelButton.Text = "Close";
                        }
                        completionSource.TrySetException(ex);
                        Application.RequestStop(dialog);
                    });
                }
            });

            // Run the dialog modally - this will block until transfer completes or is cancelled
            Application.Run(dialog);

            // Wait for completion
            try
            {
                await completionSource.Task;
            }
            catch
            {
                // Exceptions are handled by the caller
            }
        }
    }

    /// <summary>
    /// Handle for updating the file transfer status dialog
    /// </summary>
    public class FileTransferStatusDialogHandle
    {
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
            Application.MainLoop.Invoke(() =>
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
            Application.MainLoop.Invoke(() =>
            {
                if (Dialog != null)
                {
                    Application.RequestStop(Dialog);
                }
            });
        }
    }
}