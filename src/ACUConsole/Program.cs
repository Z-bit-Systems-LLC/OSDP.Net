using System;

namespace ACUConsole
{
    /// <summary>
    /// Main program class for ACU Console using MVP pattern
    /// </summary>
    internal static class Program
    {
        private static ACUConsoleController _controller;
        private static ACUConsoleView _view;

        private static void Main()
        {
            try
            {
                // Create controller (handles business logic)
                _controller = new ACUConsoleController();
                
                // Create view (handles UI)
                _view = new ACUConsoleView(_controller);
                
                // Initialize and run the application
                _view.Initialize();
                _view.Run();
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
                _controller?.Dispose();
                _view?.Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup error: {ex.Message}");
            }
        }
    }
}