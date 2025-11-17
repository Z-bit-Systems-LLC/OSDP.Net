using Terminal.Gui;

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
        public static void Show()
        {
            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;

            var textLabel = new Label(1, 2, $"OSDP.Net\nACU Console\n\nVersion:\n{version}");

            var logo =
@"█████
    █     █    █  ███
   █      █    █   █
  █  ███  █▀▄  █   █
 █        █▄▀  █   █
█
███████████████████████";
            var logoLabel = new Label(15, 1, logo);

            void OkButtonClicked()
            {
                Application.RequestStop();
            }

            var okButton = new Button("OK", true);
            okButton.Clicked += OkButtonClicked;

            var dialog = new Dialog("About", 42, 12, okButton);
            dialog.Add(textLabel, logoLabel);
            okButton.SetFocus();

            Application.Run(dialog);
        }
    }
}
