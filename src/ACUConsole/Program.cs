using System;

namespace ACUConsole
{
    /// <summary>
    /// Main program class for ACU Console using MVP pattern
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
                
                // Create view (handles UI)
                _view = new ACUConsoleView(_presenter);
                
                // Wire up presenter and view (avoiding circular dependency)
                _presenter.SetView(_view);
                
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
                _presenter?.Dispose();
                _view?.Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup error: {ex.Message}");
            }
        }
    }
}