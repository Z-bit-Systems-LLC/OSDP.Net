using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ACUConsole.Dialogs;
using ACUConsole.Model;
using OSDP.Net.Model.CommandData;
using Terminal.Gui;

namespace ACUConsole
{
    /// <summary>
    /// View class that handles all Terminal.Gui UI elements and interactions for ACU Console
    /// </summary>
    public class ACUConsoleView
    {
        private readonly IACUConsolePresenter _presenter;

        // UI Components
        private Window _window;
        private ScrollView _scrollView;
        private FrameView _deviceStatusFrame;
        private ListView _deviceStatusList;
        private MenuBar _menuBar;
        private readonly MenuItem _discoverMenuItem;

        // Device status tracking
        private readonly Dictionary<byte, DeviceConnectionStatus> _deviceStatuses = new();

        private class DeviceConnectionStatus
        {
            public string DeviceName { get; set; }
            public byte Address { get; init; }
            public bool IsConnected { get; set; }
            public bool IsSecureChannelEstablished { get; set; }
        }
        
        public ACUConsoleView(IACUConsolePresenter presenter)
        {
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            
            // Create discover menu item that can be updated
            _discoverMenuItem = new MenuItem("_Discover", string.Empty, () => _ = DiscoverDevice());
            
            // Subscribe to presenter events
            _presenter.MessageReceived += OnMessageReceived;
            _presenter.StatusChanged += OnStatusChanged;
            _presenter.ConnectionStatusChanged += OnConnectionStatusChanged;
            _presenter.ErrorOccurred += OnErrorOccurred;
        }

        public Window CreateMainWindow()
        {
            _window = new Window("OSDP.Net ACU Console")
            {
                X = 0,
                Y = 1, // Leave one row for the toplevel menu
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1
            };

            CreateMenuBar();
            CreateScrollView();
            CreateDeviceStatusPanel();

            // Add MenuBar to Application.Top (like PDConsole does)
            Application.Top.Add(_menuBar);

            // Add ScrollView and Device Status Panel to the window
            _window.Add(_scrollView);
            _window.Add(_deviceStatusFrame);

            // Initialize device statuses from configured devices
            InitializeDeviceStatuses();

            return _window;
        }

        private void CreateMenuBar()
        {
            _menuBar = new MenuBar([
                new MenuBarItem("_System", [
                    new MenuItem("_About", "", ShowAbout),
                    null, // Separator
                    new MenuItem("_Connection Settings", "", UpdateConnectionSettings),
                    new MenuItem("_Parse OSDP Cap File", "", ParseOSDPCapFile),
                    null, // Separator
                    new MenuItem("_Load Configuration", "", LoadConfigurationSettings),
                    new MenuItem("_Save Configuration", "", SaveConfigurationSettings),
                    null, // Separator
                    new MenuItem("_Quit", "", Quit)
                ]),
                new MenuBarItem("Co_nnections", [
                    new MenuItem("Start Serial Connection", "", () => _ = StartSerialConnection()),
                    new MenuItem("Start TCP Server Connection", "", () => _ = StartTcpServerConnection()),
                    new MenuItem("Start TCP Client Connection", "", () => _ = StartTcpClientConnection()),
                    new MenuItem("Stop Connections", "", () => _ = _presenter.StopConnection())
                ]),
                new MenuBarItem("_Devices", [
                    new MenuItem("_Add", string.Empty, AddDevice),
                    new MenuItem("_Remove", string.Empty, RemoveDevice),
                    _discoverMenuItem
                ]),
                new MenuBarItem("_Commands", [
                    new MenuItem("ACU Receive Size", "", () => _ = SendACUReceiveSizeCommand()),
                    new MenuItem("Communication Configuration", "", () => _ = SendCommunicationConfiguration()),
                    new MenuItem("Biometric Read", "", () => _ = SendBiometricReadCommand()),
                    new MenuItem("Biometric Match", "", () => _ = SendBiometricMatchCommand()),
                    new MenuItem("_Device Capabilities", "", () => SendSimpleCommand("Device capabilities", _presenter.SendDeviceCapabilities)),
                    new MenuItem("Encryption Key Set", "", () => _ = SendEncryptionKeySetCommand()),
                    new MenuItem("File Transfer", "", () => _ = SendFileTransferCommand()),
                    new MenuItem("Get PIV Data", "", () => _ = SendGetPIVDataCommand()),
                    new MenuItem("_ID Report", "", () => SendSimpleCommand("ID report", _presenter.SendIdReport)),
                    new MenuItem("Input Status", "", () => SendSimpleCommand("Input status", _presenter.SendInputStatus)),
                    new MenuItem("_Local Status", "", () => SendSimpleCommand("Local Status", _presenter.SendLocalStatus)),
                    new MenuItem("Manufacturer Specific", "", () => _ = SendManufacturerSpecificCommand()),
                    new MenuItem("Output Control", "", () => _ = SendOutputControlCommand()),
                    new MenuItem("Output Status", "", () => SendSimpleCommand("Output status", _presenter.SendOutputStatus)),
                    new MenuItem("Reader Buzzer Control", "", () => _ = SendReaderBuzzerControlCommand()),
                    new MenuItem("Reader LED Control", "", () => _ = SendReaderLedControlCommand()),
                    new MenuItem("Reader Text Output", "", () => _ = SendReaderTextOutputCommand()),
                    new MenuItem("_Reader Status", "", () => SendSimpleCommand("Reader status", _presenter.SendReaderStatus))
                ]),
                new MenuBarItem("_Invalid Commands", [
                    new MenuItem("_Bad CRC/Checksum", "", () => SendCustomCommand("Bad CRC/Checksum", new Commands.InvalidCrcPollCommand())),
                    new MenuItem("Invalid Command Length", "", () => SendCustomCommand("Invalid Command Length", new Commands.InvalidLengthPollCommand())),
                    new MenuItem("Invalid Command", "", () => SendCustomCommand("Invalid Command", new Commands.InvalidCommand()))
                ])
            ]);
        }


        private void CreateScrollView()
        {
            _scrollView = new ScrollView
            {
                X = 1,
                Y = 0,
                Width = Dim.Fill() - 32,  // Leave room for device status panel (30 chars + borders)
                Height = Dim.Fill(),
                ContentSize = new Size(500, 100),
                ShowVerticalScrollIndicator = true,
                ShowHorizontalScrollIndicator = true
            };
        }

        private void CreateDeviceStatusPanel()
        {
            // Create device status frame (right side panel, 30 characters wide)
            _deviceStatusFrame = new FrameView("Device Status")
            {
                X = Pos.AnchorEnd(30),
                Y = 0,
                Width = 30,
                Height = Dim.Fill()
            };

            // Create ListView for device status display
            _deviceStatusList = new ListView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            _deviceStatusFrame.Add(_deviceStatusList);
        }

        private void InitializeDeviceStatuses()
        {
            // Initialize device statuses from configured devices
            _deviceStatuses.Clear();
            foreach (var device in _presenter.Settings.Devices)
            {
                _deviceStatuses[device.Address] = new DeviceConnectionStatus
                {
                    DeviceName = device.Name,
                    Address = device.Address,
                    IsConnected = false,
                    IsSecureChannelEstablished = false
                };
            }
            UpdateDeviceStatusDisplay();
        }

        private void UpdateDeviceStatusDisplay()
        {
            Application.MainLoop.Invoke(() =>
            {
                var displayItems = new List<string>();

                // Add a header if there are devices
                if (_deviceStatuses.Count > 0)
                {
                    displayItems.Add("Device     Addr   Con   Sec");
                    displayItems.Add("----------------------------");
                }

                // Build display list from device statuses
                displayItems.AddRange(_deviceStatuses.Values
                    .OrderBy(d => d.Address)
                    .Select(d =>
                    {
                        var connSymbol = d.IsConnected ? "●" : "○";
                        var secSymbol = d.IsSecureChannelEstablished ? "●" : "○";
                        return $"{d.DeviceName, -12}{d.Address, 3} {connSymbol, 5} {secSymbol, 5}";
                    }));

                // If no devices, show a message
                if (_deviceStatuses.Count == 0)
                {
                    displayItems.Add("No devices configured");
                }

                _deviceStatusList.SetSource(displayItems);
            });
        }

        // System Menu Actions
        private static void ShowAbout()
        {
            AboutDialog.Show();
        }

        private void Quit()
        {
            // Show save the configuration dialog before exiting
            try
            {
                var shouldSave = MessageBox.ErrorQuery("Exit Application",
                    "Save configuration before exiting?",
                    "Yes", "No");

                if (shouldSave == 0) // Yes
                {
                    // If a config file path exists, save to it, otherwise show a save dialog
                    if (!string.IsNullOrEmpty(_presenter.CurrentConfigFilePath))
                    {
                        _presenter.SaveConfiguration();
                    }
                    else
                    {
                        SaveConfigurationSettings();
                    }
                }
            }
            catch (Exception)
            {
                // If a dialog fails, fall back to auto-save
                try
                {
                    _presenter.SaveConfiguration();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not save configuration: {ex.Message}");
                }
            }

            Application.RequestStop();
        }

        // Connection Methods - Using extracted dialog classes
        private async Task StartSerialConnection()
        {
            var input = SerialConnectionDialog.Show(_presenter.Settings.SerialConnectionSettings);
            
            if (!input.WasCancelled)
            {
                try
                {
                    await _presenter.StartSerialConnection(input.PortName, input.BaudRate, input.ReplyTimeout);
                }
                catch (Exception ex)
                {
                    ShowError("Connection Error", ex.Message);
                }
            }
        }

        private async Task StartTcpServerConnection()
        {
            var input = TcpServerConnectionDialog.Show(_presenter.Settings.TcpServerConnectionSettings);
            
            if (!input.WasCancelled)
            {
                try
                {
                    await _presenter.StartTcpServerConnection(input.PortNumber, input.BaudRate, input.ReplyTimeout);
                }
                catch (Exception ex)
                {
                    ShowError("Connection Error", ex.Message);
                }
            }
        }

        private async Task StartTcpClientConnection()
        {
            var input = TcpClientConnectionDialog.Show(_presenter.Settings.TcpClientConnectionSettings);
            
            if (!input.WasCancelled)
            {
                try
                {
                    await _presenter.StartTcpClientConnection(input.Host, input.PortNumber, input.BaudRate, input.ReplyTimeout);
                }
                catch (Exception ex)
                {
                    ShowError("Connection Error", ex.Message);
                }
            }
        }

        private void UpdateConnectionSettings()
        {
            var input = ConnectionSettingsDialog.Show(_presenter.Settings.PollingInterval, _presenter.Settings.IsTracing);
            
            if (!input.WasCancelled)
            {
                _presenter.UpdateConnectionSettings(input.PollingInterval, input.IsTracing);
            }
        }

        private void ParseOSDPCapFile()
        {
            var input = ParseOSDPCapFileDialog.Show(_presenter.GetLastOsdpConfigDirectory());
            
            if (!input.WasCancelled)
            {
                try
                {
                    _presenter.ParseOSDPCapFile(input.FilePath, input.FilterAddress, input.IgnorePollsAndAcks, input.SecureKey);
                }
                catch (Exception ex)
                {
                    ShowError("Error", $"Unable to parse. {ex.Message}");
                }
            }
        }

        private void LoadConfigurationSettings()
        {
            var input = LoadConfigurationDialog.Show();

            if (!input.WasCancelled)
            {
                try
                {
                    _presenter.LoadConfiguration(input.FilePath);

                    // Reinitialize device statuses after loading new configuration
                    InitializeDeviceStatuses();

                    MessageBox.Query(40, 6, "Load Configuration",
                        $"Configuration loaded successfully from:\n{Path.GetFileName(input.FilePath)}", "OK");
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(40, 8, "Error", ex.Message, "OK");
                }
            }
        }

        private void SaveConfigurationSettings()
        {
            var input = SaveConfigurationDialog.Show(_presenter.CurrentConfigFilePath);

            if (!input.WasCancelled)
            {
                try
                {
                    _presenter.SaveConfiguration(input.FilePath);
                    MessageBox.Query(40, 6, "Save Configuration",
                        $"Configuration saved successfully to:\n{Path.GetFileName(input.FilePath)}", "OK");
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(40, 8, "Error", ex.Message, "OK");
                }
            }
        }

        // Device Management Methods - Using extracted dialog classes
        private void AddDevice()
        {
            if (!_presenter.IsConnected)
            {
                ShowError("Information", "Start a connection before adding devices.");
                return;
            }

            var input = AddDeviceDialog.Show(_presenter.Settings.Devices.ToArray());

            if (!input.WasCancelled)
            {
                try
                {
                    _presenter.AddDevice(input.Name, input.Address, input.UseCrc,
                        input.UseSecureChannel, input.SecureChannelKey);

                    // Add device to status tracking
                    _deviceStatuses[input.Address] = new DeviceConnectionStatus
                    {
                        DeviceName = input.Name,
                        Address = input.Address,
                        IsConnected = false,
                        IsSecureChannelEstablished = false
                    };

                    // Update the status display
                    UpdateDeviceStatusDisplay();
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
            }
        }

        private void RemoveDevice()
        {
            if (!_presenter.IsConnected)
            {
                ShowError("Information", "Start a connection before removing devices.");
                return;
            }

            var deviceList = _presenter.GetDeviceList();
            var input = RemoveDeviceDialog.Show(_presenter.Settings.Devices.ToArray(), deviceList);

            if (!input.WasCancelled)
            {
                try
                {
                    _presenter.RemoveDevice(input.DeviceAddress);

                    // Remove a device from status tracking
                    _deviceStatuses.Remove(input.DeviceAddress);

                    // Update the status display
                    UpdateDeviceStatusDisplay();
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
            }
        }

        private async Task DiscoverDevice()
        {
            var input = DiscoverDeviceDialog.Show(_presenter.Settings.SerialConnectionSettings.PortName);
            
            if (!input.WasCancelled)
            {
                var cancellationTokenSource = new CancellationTokenSource();

                void CancelDiscovery()
                {
                    
                    // ReSharper disable AccessToDisposedClosure
                    cancellationTokenSource?.Cancel();
                    cancellationTokenSource?.Dispose();
                    // ReSharper restore AccessToDisposedClosure
                    cancellationTokenSource = null;
                }

                void CompleteDiscovery()
                {
                    _discoverMenuItem.Title = "_Discover";
                    _discoverMenuItem.Action = () => _ = DiscoverDevice();
                }

                try
                {
                    _discoverMenuItem.Title = "Cancel _Discover";
                    _discoverMenuItem.Action = CancelDiscovery;

                    await _presenter.DiscoverDevice(input.PortName, input.PingTimeout, input.ReconnectDelay, cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // Discovery was cancelled - this is expected, no need to show an error
                }
                catch (Exception ex)
                {
                    ShowError("Exception in Device Discovery", ex.Message);
                }
                finally
                {
                    CompleteDiscovery();
                    cancellationTokenSource?.Dispose();
                }
            }
        }

        // Command Methods - Simplified
        private void SendSimpleCommand(string title, Func<byte, Task> commandFunction)
        {
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            _ = ShowDeviceSelectionDialog(title, async (address) =>
            {
                try
                {
                    await commandFunction(address);
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(40, 10, $"Error on address {address}", ex.Message, "OK");
                }
            });
        }

        private async Task SendCommunicationConfiguration()
        {
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var deviceList = _presenter.GetDeviceList();
            var input = CommunicationConfigurationDialog.Show(_presenter.Settings.Devices.ToArray(), deviceList, _presenter.Settings.SerialConnectionSettings.BaudRate);

            if (!input.WasCancelled)
            {
                try
                {
                    await _presenter.SendCommunicationConfiguration(input.DeviceAddress, input.NewAddress, input.NewBaudRate);

                    // Update device status tracking when address changes
                    if (input.DeviceAddress != input.NewAddress && _deviceStatuses.ContainsKey(input.DeviceAddress))
                    {
                        // Copy the existing status to preserve connection state
                        var existingStatus = _deviceStatuses[input.DeviceAddress];

                        // Remove the old address entry
                        _deviceStatuses.Remove(input.DeviceAddress);

                        // Add new entry with updated address
                        _deviceStatuses[input.NewAddress] = new DeviceConnectionStatus
                        {
                            DeviceName = existingStatus.DeviceName,
                            Address = input.NewAddress,
                            IsConnected = existingStatus.IsConnected,
                            IsSecureChannelEstablished = existingStatus.IsSecureChannelEstablished
                        };

                        // Update the display
                        UpdateDeviceStatusDisplay();
                    }

                    if (_presenter.Settings.SerialConnectionSettings.BaudRate != input.NewBaudRate)
                    {
                        ShowInformation("Info", $"The connection needs to be restarted with baud rate of {input.NewBaudRate}");
                    }
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
            }
        }

        private async Task SendACUReceiveSizeCommand()
        {
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var deviceList = _presenter.GetDeviceList();
            var input = ACUReceiveSizeDialog.Show(_presenter.Settings.Devices.ToArray(), deviceList);

            if (!input.WasCancelled)
            {
                try
                {
                    await _presenter.SendACUReceiveSize(input.DeviceAddress, input.MaximumReceiveSize);
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
            }
        }

        private async Task SendOutputControlCommand()
        {
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var deviceList = _presenter.GetDeviceList();
            var input = OutputControlDialog.Show(_presenter.Settings.Devices.ToArray(), deviceList);
            
            if (!input.WasCancelled)
            {
                try
                {
                    await _presenter.SendOutputControl(input.DeviceAddress, input.OutputNumber, input.ActivateOutput);
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
            }
        }

        private async Task SendReaderLedControlCommand()
        {
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var deviceList = _presenter.GetDeviceList();
            var input = ReaderLedControlDialog.Show(_presenter.Settings.Devices.ToArray(), deviceList);
            
            if (!input.WasCancelled)
            {
                try
                {
                    await _presenter.SendReaderLedControl(input.DeviceAddress, input.LedNumber, input.Color);
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
            }
        }

        private async Task SendReaderBuzzerControlCommand()
        {
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var deviceList = _presenter.GetDeviceList();
            var input = ReaderBuzzerControlDialog.Show(_presenter.Settings.Devices.ToArray(), deviceList);
            
            if (!input.WasCancelled)
            {
                try
                {
                    await _presenter.SendReaderBuzzerControl(input.DeviceAddress, input.ReaderNumber, input.RepeatTimes);
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
            }
        }

        private async Task SendReaderTextOutputCommand()
        {
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var deviceList = _presenter.GetDeviceList();
            var input = ReaderTextOutputDialog.Show(_presenter.Settings.Devices.ToArray(), deviceList);
            
            if (!input.WasCancelled)
            {
                try
                {
                    await _presenter.SendReaderTextOutput(input.DeviceAddress, input.ReaderNumber, input.Text);
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
            }
        }

        private async Task SendManufacturerSpecificCommand()
        {
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var deviceList = _presenter.GetDeviceList();
            var input = ManufacturerSpecificDialog.Show(_presenter.Settings.Devices.ToArray(), deviceList);
            
            if (!input.WasCancelled)
            {
                try
                {
                    await _presenter.SendManufacturerSpecific(input.DeviceAddress, input.VendorCode, input.Data);
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
            }
        }

        private async Task SendEncryptionKeySetCommand()
        {
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var deviceList = _presenter.GetDeviceList();
            var input = EncryptionKeySetDialog.Show(_presenter.Settings.Devices.ToArray(), deviceList);
            
            if (!input.WasCancelled)
            {
                try
                {
                    await _presenter.SendEncryptionKeySet(input.DeviceAddress, input.EncryptionKey);
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
            }
        }

        private async Task SendBiometricReadCommand()
        {
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var deviceList = _presenter.GetDeviceList();
            var input = BiometricReadDialog.Show(_presenter.Settings.Devices.ToArray(), deviceList);
            
            if (!input.WasCancelled)
            {
                try
                {
                    await _presenter.SendBiometricRead(input.DeviceAddress, input.ReaderNumber, input.Type, input.Format, input.Quality);
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
            }
        }

        private async Task SendBiometricMatchCommand()
        {
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var deviceList = _presenter.GetDeviceList();
            var input = BiometricMatchDialog.Show(_presenter.Settings.Devices.ToArray(), deviceList);

            if (!input.WasCancelled)
            {
                try
                {
                    await _presenter.SendBiometricMatch(input.DeviceAddress, input.ReaderNumber, input.Type, input.Format, input.QualityThreshold, input.TemplateData);
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
            }
        }

        private async Task SendGetPIVDataCommand()
        {
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var deviceList = _presenter.GetDeviceList();
            var input = GetPIVDataDialog.Show(_presenter.Settings.Devices.ToArray(), deviceList);

            if (!input.WasCancelled)
            {
                // Prompt user to present PIV card
                MessageBox.Query(60, 8, "Present PIV Card",
                    "Press OK and then present card to reader.",
                    "OK");

                try
                {
                    await _presenter.SendGetPIVData(input.DeviceAddress, input.ObjectId, input.ElementId, input.DataOffset);
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
            }
        }

        private async Task SendFileTransferCommand()
        {
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var deviceList = _presenter.GetDeviceList();
            var input = FileTransferDialog.Show(_presenter.Settings.Devices.ToArray(), deviceList);

            if (!input.WasCancelled)
            {
                var cancellationTokenSource = new CancellationTokenSource();

                // Define the transfer function
                async Task DoTransfer(FileTransferStatusDialogHandle statusDialogHandle)
                {
                    var result = await _presenter.SendFileTransfer(
                        input.DeviceAddress,
                        input.Type,
                        input.FileData,
                        input.MessageSize,
                        status => statusDialogHandle?.UpdateProgress(status, input.FileData.Length),
                        // ReSharper disable once AccessToDisposedClosure
                        cancellationTokenSource.Token);

                    // ReSharper disable once AccessToDisposedClosure
                    if (!cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        Application.MainLoop.Invoke(() =>
                        {
                            ShowInformation("File Transfer Complete", $"File transferred successfully in {result.FragmentCount} fragments.");
                        });
                    }
                }

                try
                {
                    // Show the dialog and perform the transfer
                    await FileTransferStatusDialog.Show(
                        () => cancellationTokenSource.Cancel(),
                        DoTransfer);
                }
                catch (OperationCanceledException)
                {
                    // Transfer was cancelled - no need to show error
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
                finally
                {
                    cancellationTokenSource.Dispose();
                }
            }
        }

        private void SendCustomCommand(string title, CommandData commandData)
        {
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            _ = ShowDeviceSelectionDialog(title, async (address) =>
            {
                try
                {
                    await _presenter.SendCustomCommand(address, commandData);
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(40, 10, $"Error on address {address}", ex.Message, "OK");
                }
            });
        }

        // Helper Methods
        private void ShowCommandRequirementsError()
        {
            if (!_presenter.IsConnected)
            {
                MessageBox.ErrorQuery(60, 10, "Warning", "Start a connection before sending commands.", "OK");
            }
            else if (_presenter.Settings.Devices.Count == 0)
            {
                MessageBox.ErrorQuery(60, 10, "Warning", "Add a device before sending commands.", "OK");
            }
        }

        private async Task ShowDeviceSelectionDialog(string title, Func<byte, Task> actionFunction)
        {
            var deviceList = _presenter.GetDeviceList();
            var deviceSelection = DeviceSelectionDialog.Show(title, _presenter.Settings.Devices.ToArray(), deviceList);
            
            if (!deviceSelection.WasCancelled)
            {
                await actionFunction(deviceSelection.SelectedDeviceAddress);
            }
        }


        // Event Handlers
        private void OnMessageReceived(object sender, ACUEvent acuEvent)
        {
            UpdateMessageDisplay();
        }

        private void OnStatusChanged(object sender, string status)
        {
            // Status updates can be displayed in a status bar if needed
        }

        private void OnConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs args)
        {
            // Filter out the configuration address (127 / 0x7F) used during discovery
            if (args.Address == 127)
            {
                return;
            }

            // Update device status tracking
            if (_deviceStatuses.ContainsKey(args.Address))
            {
                _deviceStatuses[args.Address].IsConnected = args.IsConnected;
                _deviceStatuses[args.Address].IsSecureChannelEstablished = args.IsSecureChannelEstablished;
                _deviceStatuses[args.Address].DeviceName = args.DeviceName;
            }
            else
            {
                // Device not in our tracking dictionary, add it
                _deviceStatuses[args.Address] = new DeviceConnectionStatus
                {
                    DeviceName = args.DeviceName,
                    Address = args.Address,
                    IsConnected = args.IsConnected,
                    IsSecureChannelEstablished = args.IsSecureChannelEstablished
                };
            }

            // Refresh the device status display
            UpdateDeviceStatusDisplay();
        }

        private void OnErrorOccurred(object sender, Exception ex)
        {
            Application.MainLoop.Invoke(() =>
            {
                MessageBox.ErrorQuery("Error", ex.Message, "OK");
            });
        }

        private void UpdateMessageDisplay()
        {
            Application.MainLoop.Invoke(() =>
            {
                if (!_window.HasFocus && _menuBar.HasFocus)
                {
                    return;
                }

                _scrollView.RemoveAll();

                int index = 0;
                foreach (var message in _presenter.MessageHistory.Reverse())
                {
                    var messageText = message.ToString().TrimEnd();
                    var label = new Label(0, index, messageText);
                    index += label.Bounds.Height;

                    // Color code messages based on type
                    if (messageText.Contains("| WARN |") || messageText.Contains("NAK") || message.Type == ACUEventType.Warning)
                    {
                        label.ColorScheme = new ColorScheme
                            { Normal = Terminal.Gui.Attribute.Make(Color.Black, Color.BrightYellow) };
                    }
                    else if (messageText.Contains("| ERROR |") || message.Type == ACUEventType.Error)
                    {
                        label.ColorScheme = new ColorScheme
                            { Normal = Terminal.Gui.Attribute.Make(Color.White, Color.BrightRed) };
                    }

                    _scrollView.Add(label);
                }
            });
        }

        // IACUConsoleView interface implementation
        public void ShowInformation(string title, string message)
        {
            Application.MainLoop.Invoke(() =>
            {
                MessageBox.Query(60, 8, title, message, "OK");
            });
        }

        public void ShowError(string title, string message)
        {
            Application.MainLoop.Invoke(() =>
            {
                MessageBox.ErrorQuery(60, 8, title, message, "OK");
            });
        }

        public void ShowWarning(string title, string message)
        {
            Application.MainLoop.Invoke(() =>
            {
                MessageBox.Query(60, 8, title, message, "OK");
            });
        }

        public bool AskYesNo(string title, string message)
        {
            var result = false;
            Application.MainLoop.Invoke(() =>
            {
                result = MessageBox.Query(60, 8, title, message, 1, "No", "Yes") == 1;
            });
            return result;
        }

        public void UpdateDiscoverMenuItem(string title, Action action)
        {
            Application.MainLoop.Invoke(() =>
            {
                _discoverMenuItem.Title = title;
                _discoverMenuItem.Action = action;
            });
        }

        public void RefreshMessageDisplay()
        {
            UpdateMessageDisplay();
        }

        public void Shutdown()
        {
            Application.Shutdown();
        }
    }
}