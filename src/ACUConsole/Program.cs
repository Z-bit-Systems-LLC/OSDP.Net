using System;
using Terminal.Gui;

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

                // Initialize Terminal.Gui FIRST (like PDConsole does)
                Application.Init();

                // Create view (handles UI)
                _view = new ACUConsoleView(_presenter);

                // Create and add the main window (like PDConsole)
                var mainWindow = _view.CreateMainWindow();

                Application.Top.Add(mainWindow);

                // Run the application
                Application.Run();
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
                Application.Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup error: {ex.Message}");
                Console.WriteLine($"Cleanup stack trace: {ex.StackTrace}");
            }
        }
    }
}