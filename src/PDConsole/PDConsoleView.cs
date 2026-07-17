using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace PDConsole
{
    /// <summary>
    /// View class that handles all Terminal.Gui UI elements and interactions
    /// </summary>
    public class PDConsoleView
    {
        private readonly IPDConsolePresenter _controller;
        private readonly IApplication _app;

        // UI Controls
        private Window _mainWindow;
        private Label _statusLabel;
        private Label _connectionLabel;
        private ListView _commandHistoryView;
        private TextField _cardDataField;
        private TextField _keypadField;
        private Button _sendCardButton;
        private Button _sendKeypadButton;

        public PDConsoleView(IPDConsolePresenter controller, IApplication app)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _app = app ?? throw new ArgumentNullException(nameof(app));

            // Subscribe to controller events
            _controller.CommandReceived += OnCommandReceived;
            _controller.StatusChanged += OnStatusChanged;
            _controller.ConnectionStatusChanged += OnConnectionStatusChanged;
            _controller.ErrorOccurred += OnErrorOccurred;
        }

        public Window CreateMainWindow()
        {
            _mainWindow = new Window
            {
                Title = "OSDP.Net PD Console",
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                BorderStyle = LineStyle.None
            };

            // Create the menu bar (hosted at the top of the main window)
            var menuBar = CreateMenuBar();
            menuBar.Width = Dim.Fill();

            // Create UI sections
            var statusFrame = CreateStatusFrame();
            var simulationFrame = CreateSimulationFrame();
            var historyFrame = CreateHistoryFrame();

            // Position frames below the menu bar
            statusFrame.X = 0;
            statusFrame.Y = 1;

            simulationFrame.X = 0;
            simulationFrame.Y = Pos.Bottom(statusFrame);

            historyFrame.X = 0;
            historyFrame.Y = Pos.Bottom(simulationFrame);

            _mainWindow.Add(menuBar, statusFrame, simulationFrame, historyFrame);

            return _mainWindow;
        }

        private MenuBar CreateMenuBar()
        {
            return new MenuBar([
                new MenuBarItem("_File", [
                    new MenuItem("_About", "", ShowAbout),
                    null, // Separator
                    new MenuItem("_Load Settings", "", LoadSettingsDialog),
                    new MenuItem("_Save Settings", "", SaveSettingsDialog),
                    null, // Separator
                    new MenuItem("_Quit", "", () => _app.RequestStop())
                ]),
                new MenuBarItem("_Device", [
                    new MenuItem("_Activate", "", () => _ = StartDevice()),
                    new MenuItem("S_top", "", () => _ = StopDevice()),
                    null, // Separator
                    new MenuItem("_Clear History", "", ClearHistory)
                ])
            ]);
        }

        private FrameView CreateStatusFrame()
        {
            var frame = new FrameView
            {
                Title = "Device Status",
                Width = Dim.Fill(),
                Height = 5
            };

            _connectionLabel = new Label
            {
                Text = "Connection: Not Started",
                X = 1,
                Y = 0
            };

            _statusLabel = new Label
            {
                Text = _controller.GetDeviceStatusText(),
                X = 1,
                Y = 1
            };

            frame.Add(_connectionLabel, _statusLabel);
            return frame;
        }

        private FrameView CreateSimulationFrame()
        {
            var frame = new FrameView
            {
                Title = "Simulation Controls",
                Width = Dim.Fill(),
                Height = 6
            };

            // Card data controls
            var cardDataLabel = new Label
            {
                Text = "Card Data (Binary):",
                X = 1,
                Y = 1
            };

            _cardDataField = new TextField
            {
                Text = _controller.Settings.Simulation.CardNumber,
                X = Pos.Right(cardDataLabel) + 1,
                Y = 1,
                Width = 40
            };

            _sendCardButton = new Button
            {
                Text = "Send Card",
                X = Pos.Right(_cardDataField) + 1,
                Y = 1
            };
            _sendCardButton.Accepting += (_, e) => { SendCardClicked(); e.Handled = true; };

            // Keypad controls
            var keypadLabel = new Label
            {
                Text = "Keypad Data:",
                X = 1,
                Y = 3
            };

            _keypadField = new TextField
            {
                Text = _controller.Settings.Simulation.PinNumber,
                X = Pos.Right(keypadLabel) + 1,
                Y = 3,
                Width = 20
            };

            _sendKeypadButton = new Button
            {
                Text = "Send Keypad",
                X = Pos.Right(_keypadField) + 1,
                Y = 3
            };
            _sendKeypadButton.Accepting += (_, e) => { SendKeypadClicked(); e.Handled = true; };

            frame.Add(cardDataLabel, _cardDataField, _sendCardButton,
                     keypadLabel, _keypadField, _sendKeypadButton);

            return frame;
        }

        private FrameView CreateHistoryFrame()
        {
            var frame = new FrameView
            {
                Title = "Command History",
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            _commandHistoryView = new ListView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            _commandHistoryView.Activating += (_, _) => ShowCommandDetails();

            frame.Add(_commandHistoryView);
            return frame;
        }

        // UI Event Handlers
        private async Task StartDevice()
        {
            // Show serial connection dialog first
            var input = Dialogs.SerialConnectionDialog.Show(_app, _controller.Settings.Connection);

            if (input.WasCancelled)
            {
                return;
            }

            try
            {
                // Update connection settings
                _controller.UpdateSerialConnection(input.PortName, input.BaudRate);

                // Start the device
                await _controller.StartDevice();
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery(_app,"Error", $"Failed to start device: {ex.Message}", "OK");
            }
        }

        private async Task StopDevice()
        {
            try
            {
                await _controller.StopDevice();
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery(_app,"Error", $"Failed to stop device: {ex.Message}", "OK");
            }
        }

        private void ClearHistory()
        {
            _controller.ClearHistory();
            UpdateCommandHistoryView();
        }

        private void ShowAbout()
        {
            Dialogs.AboutDialog.Show(_app);
        }

        private void SendCardClicked()
        {
            try
            {
                var cardData = _cardDataField.Text;
                _controller.SendSimulatedCardRead(cardData);
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery(_app,"Error", $"Failed to send card data: {ex.Message}", "OK");
            }
        }

        private void SendKeypadClicked()
        {
            try
            {
                var keys = _keypadField.Text;
                _controller.SimulateKeypadEntry(keys);
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery(_app,"Error", $"Failed to send keypad data: {ex.Message}", "OK");
            }
        }

        private void ShowCommandDetails()
        {
            var selected = _commandHistoryView.SelectedItem;
            if (selected >= 0 && selected < _controller.CommandHistory.Count)
            {
                var commandEvent = _controller.CommandHistory[selected.Value];
                Dialogs.CommandDetailsDialog.Show(_app, commandEvent);
            }
        }

        private void LoadSettingsDialog()
        {
            if (_controller.IsDeviceRunning)
            {
                MessageBox.ErrorQuery(_app,"Error", "Cannot load settings while device is running.\nStop the device first.", "OK");
                return;
            }

            var input = Dialogs.LoadSettingsDialog.Show(_app);

            if (!input.WasCancelled)
            {
                try
                {
                    _controller.LoadSettings(input.FilePath);

                    // Update the status label with new settings
                    if (_statusLabel != null)
                        _statusLabel.Text = _controller.GetDeviceStatusText();

                    // Update simulation fields with loaded settings
                    if (_cardDataField != null)
                        _cardDataField.Text = _controller.Settings.Simulation.CardNumber;
                    if (_keypadField != null)
                        _keypadField.Text = _controller.Settings.Simulation.PinNumber;

                    MessageBox.Query(_app,40, 6, "Load Settings",
                        $"Settings loaded successfully from:\n{Path.GetFileName(input.FilePath)}", "OK");
                }
                catch (Exception exception)
                {
                    MessageBox.ErrorQuery(_app,"Error", exception.Message, "OK");
                }
            }
        }

        private void SaveSettingsDialog()
        {
            var input = Dialogs.SaveSettingsDialog.Show(_app, _controller.CurrentSettingsFilePath);

            if (!input.WasCancelled)
            {
                try
                {
                    // Update simulation settings with current field values before saving
                    _controller.UpdateSimulationSettings(
                        _cardDataField?.Text,
                        _keypadField?.Text);

                    _controller.SaveSettings(input.FilePath);
                    MessageBox.Query(_app,40, 6, "Save Settings",
                        $"Settings saved successfully to:\n{Path.GetFileName(input.FilePath)}", "OK");
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(_app,"Error", ex.Message, "OK");
                }
            }
        }


        // Controller Event Handlers
        private void OnCommandReceived(object sender, CommandEvent e)
        {
            _app.Invoke(UpdateCommandHistoryView);
        }

        private void OnStatusChanged(object sender, string status)
        {
            _app.Invoke(() =>
            {
                // You could add a status bar or status message area if needed
            });
        }

        private void OnConnectionStatusChanged(object sender, string status)
        {
            _app.Invoke(() =>
            {
                if (_connectionLabel != null)
                    _connectionLabel.Text = $"Connection: {status}";
            });
        }

        private void OnErrorOccurred(object sender, Exception ex)
        {
            _app.Invoke(() =>
            {
                MessageBox.ErrorQuery(_app,"Error", ex.Message, "OK");
            });
        }

        // Helper Methods
        private void UpdateCommandHistoryView()
        {
            var displayItems = _controller.CommandHistory
                .Select(e => $"{e.Timestamp:T} - {e.Description}")
                .ToArray();

            _commandHistoryView.SetSource(new ObservableCollection<string>(displayItems));

            if (displayItems.Length == 0) return;

            _commandHistoryView.SelectedItem = displayItems.Length - 1;
            _commandHistoryView.EnsureSelectedItemVisible();
        }

        private void UpdateButtonStates()
        {
            var isRunning = _controller.IsDeviceRunning;

            if (_sendCardButton != null)
                _sendCardButton.Enabled = isRunning;

            if (_sendKeypadButton != null)
                _sendKeypadButton.Enabled = isRunning;
        }
    }
}
