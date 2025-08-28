using System;
using System.Linq;
using Terminal.Gui;

namespace PDConsole
{
    /// <summary>
    /// View class that handles all Terminal.Gui UI elements and interactions
    /// </summary>
    public class PDConsoleView
    {
        private readonly IPDConsoleController _controller;
        
        // UI Controls
        private Window _mainWindow;
        private Label _statusLabel;
        private Label _connectionLabel;
        private ListView _commandHistoryView;
        private TextField _cardDataField;
        private TextField _keypadField;
        private Button _sendCardButton;
        private Button _sendKeypadButton;

        public PDConsoleView(IPDConsoleController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            
            // Subscribe to controller events
            _controller.CommandReceived += OnCommandReceived;
            _controller.StatusChanged += OnStatusChanged;
            _controller.ConnectionStatusChanged += OnConnectionStatusChanged;
            _controller.ErrorOccurred += OnErrorOccurred;
        }

        public Window CreateMainWindow()
        {
            _mainWindow = new Window("OSDP.Net PD Console")
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            // Create a menu bar
            Application.Top.Add(CreateMenuBar());

            // Create UI sections
            var statusFrame = CreateStatusFrame();
            var simulationFrame = CreateSimulationFrame();
            var historyFrame = CreateHistoryFrame();

            // Position frames
            statusFrame.X = 0;
            statusFrame.Y = 0;
            
            simulationFrame.X = 0;
            simulationFrame.Y = Pos.Bottom(statusFrame);
            
            historyFrame.X = 0;
            historyFrame.Y = Pos.Bottom(simulationFrame);

            _mainWindow.Add(statusFrame, simulationFrame, historyFrame);

            return _mainWindow;
        }

        private MenuBar CreateMenuBar()
        {
            return new MenuBar([
                new MenuBarItem("_File", [
                    new MenuItem("_Settings", "", ShowSettingsDialog),
                    new MenuItem("_Quit", "", () => Application.RequestStop())
                ]),
                new MenuBarItem("_Device", [
                    new MenuItem("_Start", "", StartDevice),
                    new MenuItem("S_top", "", StopDevice),
                    new MenuItem("_Clear History", "", ClearHistory)
                ])
            ]);
        }

        private FrameView CreateStatusFrame()
        {
            var frame = new FrameView("Device Status")
            {
                Width = Dim.Fill(),
                Height = 5
            };

            _connectionLabel = new Label("Connection: Not Started")
            {
                X = 1,
                Y = 0
            };

            _statusLabel = new Label(_controller.GetDeviceStatusText())
            {
                X = 1,
                Y = 1
            };

            frame.Add(_connectionLabel, _statusLabel);
            return frame;
        }

        private FrameView CreateSimulationFrame()
        {
            var frame = new FrameView("Simulation Controls")
            {
                Width = Dim.Fill(),
                Height = 6
            };

            // Card data controls
            var cardDataLabel = new Label("Card Data (Hex):")
            {
                X = 1,
                Y = 1
            };

            _cardDataField = new TextField("0123456789ABCDEF")
            {
                X = Pos.Right(cardDataLabel) + 1,
                Y = 1,
                Width = 30
            };

            _sendCardButton = new Button("Send Card")
            {
                X = Pos.Right(_cardDataField) + 1,
                Y = 1
            };
            _sendCardButton.Clicked += SendCardClicked;

            // Keypad controls
            var keypadLabel = new Label("Keypad Data:")
            {
                X = 1,
                Y = 3
            };

            _keypadField = new TextField("1234")
            {
                X = Pos.Right(keypadLabel) + 1,
                Y = 3,
                Width = 20
            };

            _sendKeypadButton = new Button("Send Keypad")
            {
                X = Pos.Right(_keypadField) + 1,
                Y = 3
            };
            _sendKeypadButton.Clicked += SendKeypadClicked;

            frame.Add(cardDataLabel, _cardDataField, _sendCardButton,
                     keypadLabel, _keypadField, _sendKeypadButton);

            return frame;
        }

        private FrameView CreateHistoryFrame()
        {
            var frame = new FrameView("Command History")
            {
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            _commandHistoryView = new ListView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            _commandHistoryView.OpenSelectedItem += ShowCommandDetails;

            frame.Add(_commandHistoryView);
            return frame;
        }

        // UI Event Handlers
        private void StartDevice()
        {
            try
            {
                _controller.StartDevice();
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Error", $"Failed to start device: {ex.Message}", "OK");
            }
        }

        private void StopDevice()
        {
            try
            {
                _controller.StopDevice();
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Error", $"Failed to stop device: {ex.Message}", "OK");
            }
        }

        private void ClearHistory()
        {
            _controller.ClearHistory();
            UpdateCommandHistoryView();
        }

        private void SendCardClicked()
        {
            try
            {
                var cardData = _cardDataField.Text.ToString();
                _controller.SendSimulatedCardRead(cardData);
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Error", $"Failed to send card data: {ex.Message}", "OK");
            }
        }

        private void SendKeypadClicked()
        {
            try
            {
                var keys = _keypadField.Text.ToString();
                _controller.SimulateKeypadEntry(keys);
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Error", $"Failed to send keypad data: {ex.Message}", "OK");
            }
        }

        private void ShowCommandDetails(ListViewItemEventArgs e)
        {
            if (e.Item >= 0 && e.Item < _controller.CommandHistory.Count)
            {
                var commandEvent = _controller.CommandHistory[e.Item];
                ShowCommandDetailsDialog(commandEvent);
            }
        }

        private void ShowCommandDetailsDialog(CommandEvent commandEvent)
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

        private void ShowSettingsDialog()
        {
            MessageBox.Query("Settings", "Settings dialog not yet implemented.\nEdit appsettings.json manually.", "OK");
        }

        // Controller Event Handlers
        private void OnCommandReceived(object sender, CommandEvent e)
        {
            Application.MainLoop.Invoke(UpdateCommandHistoryView);
        }

        private void OnStatusChanged(object sender, string status)
        {
            Application.MainLoop.Invoke(() =>
            {
                // You could add a status bar or status message area if needed
            });
        }

        private void OnConnectionStatusChanged(object sender, string status)
        {
            Application.MainLoop.Invoke(() =>
            {
                if (_connectionLabel != null)
                    _connectionLabel.Text = $"Connection: {status}";
            });
        }

        private void OnErrorOccurred(object sender, Exception ex)
        {
            Application.MainLoop.Invoke(() =>
            {
                MessageBox.ErrorQuery("Error", ex.Message, "OK");
            });
        }

        // Helper Methods
        private void UpdateCommandHistoryView()
        {
            var displayItems = _controller.CommandHistory
                .Select(e => $"{e.Timestamp:T} - {e.Description}")
                .ToArray();
            
            _commandHistoryView.SetSource(displayItems);

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