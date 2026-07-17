using System;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

namespace ACUConsole
{
    /// <summary>
    /// Main program class for ACU Console using the MVP pattern
    /// </summary>
    internal static class Program
    {
        private static ACUConsolePresenter _presenter;
        private static ACUConsoleView _view;

        private static void Main()
        {

            try
            {
                // Create presenter (handles business logic)
                _presenter = new ACUConsolePresenter();

                // Enable the configuration system (loads the built-in themes) before creating the app.
                // NOTE: ConfigurationManager is deprecated in Terminal.Gui 2.4 in favor of
                // TuiConfigurationBuilder, but remains functional; migrate when it is removed.
#pragma warning disable CS0618
                ConfigurationManager.Enable(ConfigLocations.All);

                // Initialize Terminal.Gui (instance-based application)
                using var app = Application.Create().Init();

                // Use the classic Turbo Pascal theme for a familiar look.
                ThemeManager.Theme = "TurboPascal 5";
                ConfigurationManager.Apply();
#pragma warning restore CS0618

                // Terminal.Gui v2 does not install a synchronization context, so restore one that
                // marshals async continuations back onto the UI thread (required for any UI shown
                // after an await, e.g. capability lookups and multi-step command dialogs).
                System.Threading.SynchronizationContext.SetSynchronizationContext(
                    new TerminalGuiSynchronizationContext(app));

                // Create view (handles UI)
                _view = new ACUConsoleView(_presenter, app);

                // Create the main window (hosts the menu bar and content)
                var mainWindow = _view.CreateMainWindow();

                // Run the application
                app.Run(mainWindow);
                mainWindow.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
            }
            finally
            {
                Cleanup();
            }
        }

        private static void Cleanup()
        {
            try
            {
                _presenter?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup error: {ex.Message}");
                Console.WriteLine($"Cleanup stack trace: {ex.StackTrace}");
            }
        }
    }
}