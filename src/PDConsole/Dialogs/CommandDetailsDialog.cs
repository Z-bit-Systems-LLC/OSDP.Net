using System.Linq;
using Terminal.Gui;

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
        /// <param name="commandEvent">The command event to display details for</param>
        public static void Show(CommandEvent commandEvent)
        {
            var details = string.IsNullOrEmpty(commandEvent.Details)
                ? "No additional details available."
                : commandEvent.Details;

            var dialog = new Dialog("Command Details")
            {
                Width = Dim.Percent(80),
                Height = Dim.Percent(70)
            };

            var textView = new TextView()
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

            var okButton = new Button("OK")
            {
                X = Pos.Center(),
                Y = Pos.Bottom(dialog) - 3,
                IsDefault = true
            };
            okButton.Clicked += () => Application.RequestStop(dialog);

            dialog.Add(textView, okButton);
            dialog.AddButton(okButton);

            Application.Run(dialog);
        }
    }
}