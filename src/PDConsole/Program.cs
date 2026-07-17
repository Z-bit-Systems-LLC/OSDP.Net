using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PDConsole.Configuration;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

namespace PDConsole
{
    /// <summary>
    /// Main program class
    /// </summary>
    class Program
    {
        private static PDConsolePresenter _presenter;
        private static PDConsoleView _view;

        static void Main()
        {
            try
            {
                // Load settings
                var (settings, settingsFilePath) = LoadSettings();

                // Create controller (ViewModel)
                _presenter = new PDConsolePresenter(settings);
                _presenter.SetCurrentSettingsFilePath(settingsFilePath);

                // Enable the configuration system (loads the built-in themes) before creating the app.
                ConfigurationManager.Enable(ConfigLocations.All);

                // Initialize Terminal.Gui (instance-based application)
                using var app = Application.Create().Init();

                // Use the classic Turbo Pascal theme for a familiar look.
                ThemeManager.Theme = "TurboPascal 5";
                ConfigurationManager.Apply();

                // Terminal.Gui v2 does not install a synchronization context, so restore one that
                // marshals async continuations back onto the UI thread (required for any UI shown
                // after an await).
                System.Threading.SynchronizationContext.SetSynchronizationContext(
                    new TerminalGuiSynchronizationContext(app));

                // Create view
                _view = new PDConsoleView(_presenter, app);

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

        private static (Settings, string) LoadSettings()
        {
            const string settingsFile = "appsettings.json";

            if (File.Exists(settingsFile))
            {
                try
                {
                    var json = File.ReadAllText(settingsFile);
                    var settings = JsonSerializer.Deserialize<Settings>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter() }
                    }) ?? new Settings();
                    return (settings, Path.GetFullPath(settingsFile));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading settings: {ex.Message}");
                    return (new Settings(), Path.GetFullPath(settingsFile));
                }
            }
            else
            {
                var defaultSettings = new Settings();
                SaveSettings(defaultSettings, settingsFile);
                return (defaultSettings, Path.GetFullPath(settingsFile));
            }
        }

        private static void SaveSettings(Settings settings, string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
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
                Console.WriteLine($"Fatal error: {ex.Message}");
            }
        }
    }
}