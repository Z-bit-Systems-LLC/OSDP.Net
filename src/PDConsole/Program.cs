using System;
using System.IO;
using System.Text.Json;
using PDConsole.Configuration;
using Terminal.Gui;

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
                
                // Initialize Terminal.Gui
                Application.Init();
                
                // Create view
                _view = new PDConsoleView(_presenter);
                
                // Create and add a main window
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
                        PropertyNameCaseInsensitive = true
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
                    WriteIndented = true
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
                Application.Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
            }
        }
    }
}