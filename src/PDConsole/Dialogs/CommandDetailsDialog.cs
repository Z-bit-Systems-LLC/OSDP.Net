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

            var dialog = new Dialog
            {
                Title = "Command Details",
                Width = Dim.Percent(80),
                Height = Dim.Percent(70)
            };

            // Terminal.Gui v2 marks TextView obsolete in favor of an EditorView shipped in a
            // separate package; TextView remains fully functional for this read-only display.
#pragma warning disable CS0618
            var textView = new TextView
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(1),
                Height = Dim.Fill(2),
                ReadOnly = true,
                Text = $" Command: {commandEvent.Description}\n" +
                       $"    Time: {commandEvent.Timestamp:s} {commandEvent.Timestamp:t}\n" +
                       $"\n" +
                       $" {new string('─', 60)}\n" +
                       $"\n" +
                       string.Join("\n", details.Split('\n').Select(line => $" {line}"))
            };
#pragma warning restore CS0618

            var okButton = new Button
            {
                Text = "OK",
                IsDefault = true
            };
            okButton.Accepting += (_, e) => { app.RequestStop(); e.Handled = true; };

            dialog.Add(textView);
            dialog.AddButton(okButton);

            app.Run(dialog);
            dialog.Dispose();
        }
    }
}
