using System.Drawing;
using System.Linq;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace PDConsole.Dialogs
{
    /// <summary>
    /// Dialog for displaying detailed information about a command event
    /// </summary>
    public static class CommandDetailsDialog
    {
        /// <summary>
        /// Shows the command details dialog for the specified command event
        /// </summary>
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="commandEvent">The command event to display details for</param>
        public static void Show(IApplication app, CommandEvent commandEvent)
        {
            var details = string.IsNullOrEmpty(commandEvent.Details)
                ? "No additional details available."
                : commandEvent.Details;

            var text = $" Command: {commandEvent.Description}\n" +
                       $"    Time: {commandEvent.Timestamp:s} {commandEvent.Timestamp:t}\n" +
                       $"\n" +
                       $" {new string('─', 60)}\n" +
                       $"\n" +
                       string.Join("\n", details.Split('\n').Select(line => $" {line}"));

            var dialog = new Dialog
            {
                Title = "Command Details",
                Width = Dim.Percent(80),
                Height = Dim.Percent(70)
            };

            // Read-only, scrollable text display: a Label inside a scrollable View
            // (the v2 replacement for the now-obsolete read-only TextView).
            var lines = text.Split('\n');
            var contentSize = new Size(lines.Length == 0 ? 1 : lines.Max(line => line.Length), lines.Length);

            var contentLabel = new Label { X = 0, Y = 0, Text = text };

            var scrollView = new View
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(1),
                Height = Dim.Fill(2),
                CanFocus = true,
                ViewportSettings = ViewportSettingsFlags.HasVerticalScrollBar | ViewportSettingsFlags.HasHorizontalScrollBar
            };
            scrollView.SetContentSize(contentSize);
            scrollView.Add(contentLabel);

            var okButton = new Button
            {
                Text = "OK",
                IsDefault = true
            };
            okButton.Accepting += (_, e) => { app.RequestStop(); e.Handled = true; };

            dialog.Add(scrollView);
            dialog.AddButton(okButton);

            app.Run(dialog);
            dialog.Dispose();
        }
    }
}
