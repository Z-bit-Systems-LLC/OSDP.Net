using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for displaying application information with properly aligned logo
    /// </summary>
    public static class AboutDialog
    {
        /// <summary>
        /// Shows the about dialog with version information and logo
        /// </summary>
        public static void Show(IApplication app)
        {
            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;

            var textLabel = new Label
            {
                X = 1,
                Y = 2,
                Text = $"OSDP.Net\nACU Console\n\nVersion:\n{version}"
            };

            var logo =
@"█████
    █     █    █  ███
   █      █        █
  █  ███  █▀▄  █   █
 █        █▄▀  █   █
█
███████████████████████";
            var logoLabel = new Label
            {
                X = 15,
                Y = 1,
                Text = logo
            };

            var okButton = new Button { Text = "OK", IsDefault = true };
            okButton.Accepting += (_, e) => { app.RequestStop(); e.Handled = true; };

            var dialog = new Dialog
            {
                Title = "About",
                Width = 42,
                Height = Dim.Auto()
            };
            dialog.Add(textLabel, logoLabel);
            dialog.AddButton(okButton);
            okButton.SetFocus();

            app.Run(dialog);
            dialog.Dispose();
        }
    }
}
