using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ACUConsole.Configuration;
using ACUConsole.Model;
using OSDP.Net.Model.CommandData;
using NStack;
using Terminal.Gui;

namespace ACUConsole
{
    /// <summary>
    /// View class that handles all Terminal.Gui UI elements and interactions for ACU Console
    /// </summary>
    public class ACUConsoleView
    {
        private readonly IACUConsoleController _controller;
        
        // UI Components
        private Window _window;
        private ScrollView _scrollView;
        private MenuBar _menuBar;
        private readonly MenuItem _discoverMenuItem;
        
        public ACUConsoleView(IACUConsoleController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            
            // Create discover menu item that can be updated
            _discoverMenuItem = new MenuItem("_Discover", string.Empty, DiscoverDevice);
            
            // Subscribe to controller events
            _controller.MessageReceived += OnMessageReceived;
            _controller.StatusChanged += OnStatusChanged;
            _controller.ConnectionStatusChanged += OnConnectionStatusChanged;
            _controller.ErrorOccurred += OnErrorOccurred;
        }

        public void Initialize()
        {
            Application.Init();
            CreateMainWindow();
            CreateMenuBar();
            CreateScrollView();
            Application.Top.Add(_menuBar, _window);
        }

        public void Run()
        {
            Application.Run();
        }

        private void CreateMainWindow()
        {
            _window = new Window("OSDP.Net ACU Console")
            {
                X = 0,
                Y = 1, // Leave one row for the toplevel menu
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1
            };
        }

        private void CreateMenuBar()
        {
            _menuBar = new MenuBar(new[]
            {
                new MenuBarItem("_System", new[]
                {
                    new MenuItem("_About", "", ShowAbout),
                    new MenuItem("_Connection Settings", "", UpdateConnectionSettings),
                    new MenuItem("_Parse OSDP Cap File", "", ParseOSDPCapFile),
                    new MenuItem("_Load Configuration", "", LoadConfigurationSettings),
                    new MenuItem("_Save Configuration", "", () => _controller.SaveConfiguration()),
                    new MenuItem("_Quit", "", Quit)
                }),
                new MenuBarItem("Co_nnections", new[]
                {
                    new MenuItem("Start Serial Connection", "", StartSerialConnection),
                    new MenuItem("Start TCP Server Connection", "", StartTcpServerConnection),
                    new MenuItem("Start TCP Client Connection", "", StartTcpClientConnection),
                    new MenuItem("Stop Connections", "", () => _ = _controller.StopConnection())
                }),
                new MenuBarItem("_Devices", new[]
                {
                    new MenuItem("_Add", string.Empty, AddDevice),
                    new MenuItem("_Remove", string.Empty, RemoveDevice),
                    _discoverMenuItem,
                }),
                new MenuBarItem("_Commands", new[]
                {
                    new MenuItem("Communication Configuration", "", SendCommunicationConfiguration),
                    new MenuItem("Biometric Read", "", SendBiometricReadCommand),
                    new MenuItem("Biometric Match", "", SendBiometricMatchCommand),
                    new MenuItem("_Device Capabilities", "", () => SendSimpleCommand("Device capabilities", _controller.SendDeviceCapabilities)),
                    new MenuItem("Encryption Key Set", "", SendEncryptionKeySetCommand),
                    new MenuItem("File Transfer", "", SendFileTransferCommand),
                    new MenuItem("_ID Report", "", () => SendSimpleCommand("ID report", _controller.SendIdReport)),
                    new MenuItem("Input Status", "", () => SendSimpleCommand("Input status", _controller.SendInputStatus)),
                    new MenuItem("_Local Status", "", () => SendSimpleCommand("Local Status", _controller.SendLocalStatus)),
                    new MenuItem("Manufacturer Specific", "", SendManufacturerSpecificCommand),
                    new MenuItem("Output Control", "", SendOutputControlCommand),
                    new MenuItem("Output Status", "", () => SendSimpleCommand("Output status", _controller.SendOutputStatus)),
                    new MenuItem("Reader Buzzer Control", "", SendReaderBuzzerControlCommand),
                    new MenuItem("Reader LED Control", "", SendReaderLedControlCommand),
                    new MenuItem("Reader Text Output", "", SendReaderTextOutputCommand),
                    new MenuItem("_Reader Status", "", () => SendSimpleCommand("Reader status", _controller.SendReaderStatus))
                }),
                new MenuBarItem("_Invalid Commands", new[]
                {
                    new MenuItem("_Bad CRC/Checksum", "", () => SendCustomCommand("Bad CRC/Checksum", new ACUConsole.Commands.InvalidCrcPollCommand())),
                    new MenuItem("Invalid Command Length", "", () => SendCustomCommand("Invalid Command Length", new ACUConsole.Commands.InvalidLengthPollCommand())),
                    new MenuItem("Invalid Command", "", () => SendCustomCommand("Invalid Command", new ACUConsole.Commands.InvalidCommand()))
                })
            });
        }

        private void CreateScrollView()
        {
            _scrollView = new ScrollView(new Rect(0, 0, 0, 0))
            {
                ContentSize = new Size(500, 100),
                ShowVerticalScrollIndicator = true,
                ShowHorizontalScrollIndicator = true
            };
            _window.Add(_scrollView);
        }

        // System Menu Actions
        private void ShowAbout()
        {
            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            MessageBox.Query(40, 6, "About", $"OSDP.Net ACU Console\nVersion: {version}", 0, "OK");
        }

        private void Quit()
        {
            var result = MessageBox.Query(60, 8, "Exit Application", 
                "Do you want to save your configuration before exiting?", 
                2, "Cancel", "Don't Save", "Save");

            switch (result)
            {
                case 0: // Cancel
                    // Do nothing, stay in application
                    break;
                case 1: // Don't Save
                    Application.Shutdown();
                    break;
                case 2: // Save
                    _controller.SaveConfiguration();
                    Application.Shutdown();
                    break;
            }
        }

        // Connection Methods - Simplified implementations
        private void StartSerialConnection()
        {
            var portNameComboBox = CreatePortNameComboBox(15, 1);
            var baudRateTextField = new TextField(25, 3, 25, _controller.Settings.SerialConnectionSettings.BaudRate.ToString());
            var replyTimeoutTextField = new TextField(25, 5, 25, _controller.Settings.SerialConnectionSettings.ReplyTimeout.ToString());

            async void StartConnectionButtonClicked()
            {
                if (string.IsNullOrEmpty(portNameComboBox.Text.ToString()))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "No port name entered!", "OK");
                    return;
                }
                
                if (!int.TryParse(baudRateTextField.Text.ToString(), out var baudRate))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid baud rate entered!", "OK");
                    return;
                }

                if (!int.TryParse(replyTimeoutTextField.Text.ToString(), out var replyTimeout))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reply timeout entered!", "OK");
                    return;
                }

                try
                {
                    await _controller.StartSerialConnection(portNameComboBox.Text.ToString(), baudRate, replyTimeout);
                    Application.RequestStop();
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(60, 10, "Connection Error", ex.Message, "OK");
                }
            }

            var startButton = new Button("Start", true);
            startButton.Clicked += StartConnectionButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Start Serial Connection", 70, 12, cancelButton, startButton);
            dialog.Add(new Label(1, 1, "Port:"), portNameComboBox,
                      new Label(1, 3, "Baud Rate:"), baudRateTextField,
                      new Label(1, 5, "Reply Timeout(ms):"), replyTimeoutTextField);
            portNameComboBox.SetFocus();

            Application.Run(dialog);
        }

        private void StartTcpServerConnection()
        {
            var portNumberTextField = new TextField(25, 1, 25, _controller.Settings.TcpServerConnectionSettings.PortNumber.ToString());
            var baudRateTextField = new TextField(25, 3, 25, _controller.Settings.TcpServerConnectionSettings.BaudRate.ToString());
            var replyTimeoutTextField = new TextField(25, 5, 25, _controller.Settings.TcpServerConnectionSettings.ReplyTimeout.ToString());

            async void StartConnectionButtonClicked()
            {
                if (!int.TryParse(portNumberTextField.Text.ToString(), out var portNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid port number entered!", "OK");
                    return;
                }

                if (!int.TryParse(baudRateTextField.Text.ToString(), out var baudRate))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid baud rate entered!", "OK");
                    return;
                }

                if (!int.TryParse(replyTimeoutTextField.Text.ToString(), out var replyTimeout))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reply timeout entered!", "OK");
                    return;
                }

                try
                {
                    await _controller.StartTcpServerConnection(portNumber, baudRate, replyTimeout);
                    Application.RequestStop();
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(60, 10, "Connection Error", ex.Message, "OK");
                }
            }

            var startButton = new Button("Start", true);
            startButton.Clicked += StartConnectionButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Start TCP Server Connection", 60, 12, cancelButton, startButton);
            dialog.Add(new Label(1, 1, "Port Number:"), portNumberTextField,
                      new Label(1, 3, "Baud Rate:"), baudRateTextField,
                      new Label(1, 5, "Reply Timeout(ms):"), replyTimeoutTextField);
            portNumberTextField.SetFocus();

            Application.Run(dialog);
        }

        private void StartTcpClientConnection()
        {
            var hostTextField = new TextField(15, 1, 35, _controller.Settings.TcpClientConnectionSettings.Host);
            var portNumberTextField = new TextField(25, 3, 25, _controller.Settings.TcpClientConnectionSettings.PortNumber.ToString());
            var baudRateTextField = new TextField(25, 5, 25, _controller.Settings.TcpClientConnectionSettings.BaudRate.ToString());
            var replyTimeoutTextField = new TextField(25, 7, 25, _controller.Settings.TcpClientConnectionSettings.ReplyTimeout.ToString());

            async void StartConnectionButtonClicked()
            {
                if (!int.TryParse(portNumberTextField.Text.ToString(), out var portNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid port number entered!", "OK");
                    return;
                }

                if (!int.TryParse(baudRateTextField.Text.ToString(), out var baudRate))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid baud rate entered!", "OK");
                    return;
                }
                
                if (!int.TryParse(replyTimeoutTextField.Text.ToString(), out var replyTimeout))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reply timeout entered!", "OK");
                    return;
                }

                try
                {
                    await _controller.StartTcpClientConnection(hostTextField.Text.ToString(), portNumber, baudRate, replyTimeout);
                    Application.RequestStop();
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(60, 10, "Connection Error", ex.Message, "OK");
                }
            }

            var startButton = new Button("Start", true);
            startButton.Clicked += StartConnectionButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Start TCP Client Connection", 60, 15, cancelButton, startButton);
            dialog.Add(new Label(1, 1, "Host Name:"), hostTextField,
                      new Label(1, 3, "Port Number:"), portNumberTextField,
                      new Label(1, 5, "Baud Rate:"), baudRateTextField,
                      new Label(1, 7, "Reply Timeout(ms):"), replyTimeoutTextField);
            hostTextField.SetFocus();

            Application.Run(dialog);
        }

        private void UpdateConnectionSettings()
        {
            var pollingIntervalTextField = new TextField(25, 4, 25, _controller.Settings.PollingInterval.ToString());
            var tracingCheckBox = new CheckBox(1, 6, "Write packet data to file", _controller.Settings.IsTracing);

            void UpdateConnectionSettingsButtonClicked()
            {
                if (!int.TryParse(pollingIntervalTextField.Text.ToString(), out var pollingInterval))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid polling interval entered!", "OK");
                    return;
                }

                _controller.UpdateConnectionSettings(pollingInterval, tracingCheckBox.Checked);
                Application.RequestStop();
            }

            var updateButton = new Button("Update", true);
            updateButton.Clicked += UpdateConnectionSettingsButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Update Connection Settings", 60, 12, cancelButton, updateButton);
            dialog.Add(new Label(new Rect(1, 1, 55, 2), "Connection will need to be restarted for setting to take effect."),
                      new Label(1, 4, "Polling Interval(ms):"), pollingIntervalTextField,
                      tracingCheckBox);
            pollingIntervalTextField.SetFocus();

            Application.Run(dialog);
        }

        private void ParseOSDPCapFile()
        {
            var openDialog = new OpenDialog("Load OSDPCap File", string.Empty, new() { ".osdpcap" });
            Application.Run(openDialog);

            if (openDialog.Canceled || !File.Exists(openDialog.FilePath?.ToString()))
            {
                return;
            }

            var filePath = openDialog.FilePath.ToString();
            var addressTextField = new TextField(30, 1, 20, string.Empty);
            var ignorePollsAndAcksCheckBox = new CheckBox(1, 3, "Ignore Polls And Acks", false);
            var keyTextField = new TextField(15, 5, 35, Convert.ToHexString(DeviceSetting.DefaultKey));

            void ParseButtonClicked()
            {
                byte? address = null;
                if (!string.IsNullOrWhiteSpace(addressTextField.Text.ToString()))
                {
                    if (!byte.TryParse(addressTextField.Text.ToString(), out var addr) || addr > 127)
                    {
                        MessageBox.ErrorQuery(40, 10, "Error", "Invalid address entered!", "OK");
                        return;
                    }
                    address = addr;
                }

                if (keyTextField.Text != null && keyTextField.Text.Length != 32)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid key length entered!", "OK");
                    return;
                }

                byte[] key;
                try
                {
                    key = keyTextField.Text != null ? Convert.FromHexString(keyTextField.Text.ToString()!) : null;
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid hex characters!", "OK");
                    return;
                }

                try
                {
                    _controller.ParseOSDPCapFile(filePath, address, ignorePollsAndAcksCheckBox.Checked, key);
                    Application.RequestStop();
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", $"Unable to parse. {ex.Message}", "OK");
                }
            }

            var parseButton = new Button("Parse", true);
            parseButton.Clicked += ParseButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Parse settings", 60, 13, cancelButton, parseButton);
            dialog.Add(new Label(1, 1, "Filter Specific Address:"), addressTextField,
                      ignorePollsAndAcksCheckBox,
                      new Label(1, 5, "Secure Key:"), keyTextField);
            addressTextField.SetFocus();

            Application.Run(dialog);
        }

        private void LoadConfigurationSettings()
        {
            var openDialog = new OpenDialog("Load Configuration", string.Empty, new() { ".config" });
            Application.Run(openDialog);

            if (!openDialog.Canceled && File.Exists(openDialog.FilePath?.ToString()))
            {
                try
                {
                    _controller.LoadConfiguration();
                    MessageBox.Query(40, 6, "Load Configuration", "Load completed successfully", "OK");
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(40, 8, "Error", ex.Message, "OK");
                }
            }
        }

        // Device Management Methods - Simplified
        private void AddDevice()
        {
            if (!_controller.IsConnected)
            {
                MessageBox.ErrorQuery(60, 12, "Information", "Start a connection before adding devices.", "OK");
                return;
            }

            var nameTextField = new TextField(15, 1, 35, string.Empty);
            var addressTextField = new TextField(15, 3, 35, string.Empty);
            var useCrcCheckBox = new CheckBox(1, 5, "Use CRC", true);
            var useSecureChannelCheckBox = new CheckBox(1, 6, "Use Secure Channel", true);
            var keyTextField = new TextField(15, 8, 35, Convert.ToHexString(DeviceSetting.DefaultKey));

            void AddDeviceButtonClicked()
            {
                if (!byte.TryParse(addressTextField.Text.ToString(), out var address) || address > 127)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid address entered!", "OK");
                    return;
                }

                if (keyTextField.Text == null || keyTextField.Text.Length != 32)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid key length entered!", "OK");
                    return;
                }

                byte[] key;
                try
                {
                    key = Convert.FromHexString(keyTextField.Text.ToString()!);
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid hex characters!", "OK");
                    return;
                }

                var existingDevice = _controller.Settings.Devices.FirstOrDefault(d => d.Address == address);
                if (existingDevice != null)
                {
                    if (MessageBox.Query(60, 10, "Overwrite", "Device already exists at that address, overwrite?", 1, "No", "Yes") == 0)
                    {
                        return;
                    }
                }

                try
                {
                    _controller.AddDevice(nameTextField.Text.ToString(), address, useCrcCheckBox.Checked, 
                        useSecureChannelCheckBox.Checked, key);
                    Application.RequestStop();
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(60, 10, "Error", ex.Message, "OK");
                }
            }

            var addButton = new Button("Add", true);
            addButton.Clicked += AddDeviceButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Add Device", 60, 13, cancelButton, addButton);
            dialog.Add(new Label(1, 1, "Name:"), nameTextField,
                      new Label(1, 3, "Address:"), addressTextField,
                      useCrcCheckBox,
                      useSecureChannelCheckBox,
                      new Label(1, 8, "Secure Key:"), keyTextField);
            nameTextField.SetFocus();

            Application.Run(dialog);
        }

        private void RemoveDevice()
        {
            if (!_controller.IsConnected)
            {
                MessageBox.ErrorQuery(60, 10, "Information", "Start a connection before removing devices.", "OK");
                return;
            }

            var deviceList = _controller.GetDeviceList();
            if (deviceList.Length == 0)
            {
                MessageBox.ErrorQuery(60, 10, "Information", "No devices to remove.", "OK");
                return;
            }

            var scrollView = new ScrollView(new Rect(6, 1, 50, 6))
            {
                ContentSize = new Size(40, deviceList.Length * 2),
                ShowVerticalScrollIndicator = deviceList.Length > 6,
                ShowHorizontalScrollIndicator = false
            };

            var deviceRadioGroup = new RadioGroup(0, 0, deviceList.Select(ustring.Make).ToArray());
            scrollView.Add(deviceRadioGroup);

            void RemoveDeviceButtonClicked()
            {
                var selectedDevice = _controller.Settings.Devices.OrderBy(d => d.Address).ToArray()[deviceRadioGroup.SelectedItem];
                try
                {
                    _controller.RemoveDevice(selectedDevice.Address);
                    Application.RequestStop();
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(60, 10, "Error", ex.Message, "OK");
                }
            }

            var removeButton = new Button("Remove", true);
            removeButton.Clicked += RemoveDeviceButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Remove Device", 60, 13, cancelButton, removeButton);
            dialog.Add(scrollView);
            removeButton.SetFocus();

            Application.Run(dialog);
        }

        private void DiscoverDevice()
        {
            var portNameComboBox = CreatePortNameComboBox(15, 1);
            var pingTimeoutTextField = new TextField(25, 3, 25, "1000");
            var reconnectDelayTextField = new TextField(25, 5, 25, "0");

            async void OnClickDiscover()
            {
                if (string.IsNullOrEmpty(portNameComboBox.Text.ToString()))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "No port name entered!", "OK");
                    return;
                }

                if (!int.TryParse(pingTimeoutTextField.Text.ToString(), out var pingTimeout))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reply timeout entered!", "OK");
                    return;
                }

                if (!int.TryParse(reconnectDelayTextField.Text.ToString(), out var reconnectDelay))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reconnect delay entered!", "OK");
                    return;
                }

                Application.RequestStop();

                var cancellationTokenSource = new CancellationTokenSource();

                void CancelDiscovery()
                {
                    cancellationTokenSource?.Cancel();
                    cancellationTokenSource?.Dispose();
                    cancellationTokenSource = null;
                }

                void CompleteDiscovery()
                {
                    _discoverMenuItem.Title = "_Discover";
                    _discoverMenuItem.Action = DiscoverDevice;
                }

                try
                {
                    _discoverMenuItem.Title = "Cancel _Discover";
                    _discoverMenuItem.Action = CancelDiscovery;

                    await _controller.DiscoverDevice(portNameComboBox.Text.ToString(), pingTimeout, reconnectDelay, cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // Discovery was cancelled - this is expected, no need to show error
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(40, 10, "Exception in Device Discovery", ex.Message, "OK");
                }
                finally
                {
                    CompleteDiscovery();
                    cancellationTokenSource?.Dispose();
                }
            }

            var discoverButton = new Button("Discover", true);
            discoverButton.Clicked += OnClickDiscover;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Discover Device", 60, 11, cancelButton, discoverButton);
            dialog.Add(new Label(1, 1, "Port:"), portNameComboBox,
                      new Label(1, 3, "Ping Timeout(ms):"), pingTimeoutTextField,
                      new Label(1, 5, "Reconnect Delay(ms):"), reconnectDelayTextField);
            pingTimeoutTextField.SetFocus();

            Application.Run(dialog);
        }

        // Command Methods - Simplified
        private void SendSimpleCommand(string title, Func<byte, Task> commandFunction)
        {
            if (!_controller.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            ShowDeviceSelectionDialog(title, async (address) =>
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

        private void SendCommunicationConfiguration()
        {
            if (!_controller.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var newAddressTextField = new TextField(25, 1, 25, 
                ((_controller.Settings.Devices.MaxBy(device => device.Address)?.Address ?? 0) + 1).ToString());
            var newBaudRateTextField = new TextField(25, 3, 25, _controller.Settings.SerialConnectionSettings.BaudRate.ToString());

            void SendCommunicationConfigurationButtonClicked()
            {
                if (!byte.TryParse(newAddressTextField.Text.ToString(), out var newAddress) || newAddress > 127)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid address entered (0-127)!", "OK");
                    return;
                }

                if (!int.TryParse(newBaudRateTextField.Text.ToString(), out var newBaudRate))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid baud rate entered!", "OK");
                    return;
                }

                Application.RequestStop();

                ShowDeviceSelectionDialog("Communication Configuration", async (address) =>
                {
                    try
                    {
                        await _controller.SendCommunicationConfiguration(address, newAddress, newBaudRate);
                        
                        if (_controller.Settings.SerialConnectionSettings.BaudRate != newBaudRate)
                        {
                            MessageBox.Query(60, 10, "Info",
                                $"The connection needs to be restarted with baud rate of {newBaudRate}", "OK");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.ErrorQuery(60, 10, "Error", ex.Message, "OK");
                    }
                });
            }

            var sendButton = new Button("Send", true);
            sendButton.Clicked += SendCommunicationConfigurationButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Communication Configuration", 70, 12, cancelButton, sendButton);
            dialog.Add(new Label(1, 1, "New Address:"), newAddressTextField,
                      new Label(1, 3, "New Baud Rate:"), newBaudRateTextField);
            newAddressTextField.SetFocus();

            Application.Run(dialog);
        }

        private void SendOutputControlCommand()
        {
            if (!_controller.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var outputNumberTextField = new TextField(25, 1, 25, "0");
            var activateOutputCheckBox = new CheckBox(1, 3, "Activate Output", false);

            void SendOutputControlButtonClicked()
            {
                if (!byte.TryParse(outputNumberTextField.Text.ToString(), out var outputNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid output number entered!", "OK");
                    return;
                }

                Application.RequestStop();

                ShowDeviceSelectionDialog("Output Control", async (address) =>
                {
                    try
                    {
                        await _controller.SendOutputControl(address, outputNumber, activateOutputCheckBox.Checked);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.ErrorQuery(60, 10, "Error", ex.Message, "OK");
                    }
                });
            }

            var sendButton = new Button("Send", true);
            sendButton.Clicked += SendOutputControlButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Output Control", 60, 10, cancelButton, sendButton);
            dialog.Add(new Label(1, 1, "Output Number:"), outputNumberTextField,
                      activateOutputCheckBox);
            outputNumberTextField.SetFocus();

            Application.Run(dialog);
        }

        private void SendReaderLedControlCommand()
        {
            if (!_controller.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var ledNumberTextField = new TextField(25, 1, 25, "0");
            var colorComboBox = new ComboBox(new Rect(25, 3, 25, 8), new[] { "Black", "Red", "Green", "Amber", "Blue", "Magenta", "Cyan", "White" })
            {
                SelectedItem = 1 // Default to Red
            };

            void SendReaderLedControlButtonClicked()
            {
                if (!byte.TryParse(ledNumberTextField.Text.ToString(), out var ledNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid LED number entered!", "OK");
                    return;
                }

                var selectedColor = (LedColor)colorComboBox.SelectedItem;
                Application.RequestStop();

                ShowDeviceSelectionDialog("Reader LED Control", async (address) =>
                {
                    try
                    {
                        await _controller.SendReaderLedControl(address, ledNumber, selectedColor);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.ErrorQuery(60, 10, "Error", ex.Message, "OK");
                    }
                });
            }

            var sendButton = new Button("Send", true);
            sendButton.Clicked += SendReaderLedControlButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Reader LED Control", 60, 12, cancelButton, sendButton);
            dialog.Add(new Label(1, 1, "LED Number:"), ledNumberTextField,
                      new Label(1, 3, "Color:"), colorComboBox);
            ledNumberTextField.SetFocus();

            Application.Run(dialog);
        }

        private void SendReaderBuzzerControlCommand()
        {
            if (!_controller.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var readerNumberTextField = new TextField(25, 1, 25, "0");
            var repeatTimesTextField = new TextField(25, 3, 25, "1");

            void SendReaderBuzzerControlButtonClicked()
            {
                if (!byte.TryParse(readerNumberTextField.Text.ToString(), out var readerNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reader number entered!", "OK");
                    return;
                }

                if (!byte.TryParse(repeatTimesTextField.Text.ToString(), out var repeatTimes))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid repeat times entered!", "OK");
                    return;
                }

                Application.RequestStop();

                ShowDeviceSelectionDialog("Reader Buzzer Control", async (address) =>
                {
                    try
                    {
                        await _controller.SendReaderBuzzerControl(address, readerNumber, repeatTimes);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.ErrorQuery(60, 10, "Error", ex.Message, "OK");
                    }
                });
            }

            var sendButton = new Button("Send", true);
            sendButton.Clicked += SendReaderBuzzerControlButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Reader Buzzer Control", 60, 11, cancelButton, sendButton);
            dialog.Add(new Label(1, 1, "Reader Number:"), readerNumberTextField,
                      new Label(1, 3, "Repeat Times:"), repeatTimesTextField);
            readerNumberTextField.SetFocus();

            Application.Run(dialog);
        }

        private void SendReaderTextOutputCommand()
        {
            if (!_controller.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var readerNumberTextField = new TextField(25, 1, 25, "0");
            var textTextField = new TextField(25, 3, 40, "Hello World");

            void SendReaderTextOutputButtonClicked()
            {
                if (!byte.TryParse(readerNumberTextField.Text.ToString(), out var readerNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reader number entered!", "OK");
                    return;
                }

                var text = textTextField.Text.ToString();
                if (string.IsNullOrEmpty(text))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Please enter text to display!", "OK");
                    return;
                }

                Application.RequestStop();

                ShowDeviceSelectionDialog("Reader Text Output", async (address) =>
                {
                    try
                    {
                        await _controller.SendReaderTextOutput(address, readerNumber, text);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.ErrorQuery(60, 10, "Error", ex.Message, "OK");
                    }
                });
            }

            var sendButton = new Button("Send", true);
            sendButton.Clicked += SendReaderTextOutputButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Reader Text Output", 70, 11, cancelButton, sendButton);
            dialog.Add(new Label(1, 1, "Reader Number:"), readerNumberTextField,
                      new Label(1, 3, "Text:"), textTextField);
            readerNumberTextField.SetFocus();

            Application.Run(dialog);
        }

        private void SendManufacturerSpecificCommand()
        {
            if (!_controller.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var vendorCodeTextField = new TextField(25, 1, 25, "");
            var dataTextField = new TextField(25, 3, 40, "");

            void SendManufacturerSpecificButtonClicked()
            {
                byte[] vendorCode;
                try
                {
                    var vendorCodeStr = vendorCodeTextField.Text.ToString();
                    if (string.IsNullOrWhiteSpace(vendorCodeStr))
                    {
                        MessageBox.ErrorQuery(40, 10, "Error", "Please enter vendor code!", "OK");
                        return;
                    }
                    vendorCode = Convert.FromHexString(vendorCodeStr);
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid vendor code hex format!", "OK");
                    return;
                }

                if (vendorCode.Length != 3)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Vendor code must be exactly 3 bytes!", "OK");
                    return;
                }

                byte[] data;
                try
                {
                    var dataStr = dataTextField.Text.ToString();
                    if (string.IsNullOrWhiteSpace(dataStr))
                    {
                        data = Array.Empty<byte>();
                    }
                    else
                    {
                        data = Convert.FromHexString(dataStr);
                    }
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid data hex format!", "OK");
                    return;
                }

                Application.RequestStop();

                ShowDeviceSelectionDialog("Manufacturer Specific", async (address) =>
                {
                    try
                    {
                        await _controller.SendManufacturerSpecific(address, vendorCode, data);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.ErrorQuery(60, 10, "Error", ex.Message, "OK");
                    }
                });
            }

            var sendButton = new Button("Send", true);
            sendButton.Clicked += SendManufacturerSpecificButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Manufacturer Specific", 70, 13, cancelButton, sendButton);
            dialog.Add(new Label(1, 1, "Vendor Code (3 bytes hex):"), vendorCodeTextField,
                      new Label(1, 3, "Data (hex):"), dataTextField,
                      new Label(1, 5, "Example: Vendor Code = 'AABBCC', Data = '01020304'"));
            vendorCodeTextField.SetFocus();

            Application.Run(dialog);
        }

        private void SendEncryptionKeySetCommand()
        {
            if (!_controller.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var keyTextField = new TextField(1, 3, 35, "");

            void SendEncryptionKeySetButtonClicked()
            {
                var keyStr = keyTextField.Text.ToString();
                if (string.IsNullOrWhiteSpace(keyStr))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Please enter encryption key!", "OK");
                    return;
                }

                byte[] key;
                try
                {
                    key = Convert.FromHexString(keyStr);
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid hex format!", "OK");
                    return;
                }

                if (key.Length != 16)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Key must be exactly 16 bytes (32 hex chars)!", "OK");
                    return;
                }

                Application.RequestStop();

                ShowDeviceSelectionDialog("Encryption Key Set", async (address) =>
                {
                    try
                    {
                        await _controller.SendEncryptionKeySet(address, key);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.ErrorQuery(60, 10, "Error", ex.Message, "OK");
                    }
                });
            }

            var sendButton = new Button("Send", true);
            sendButton.Clicked += SendEncryptionKeySetButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Encryption Key Set", 60, 12, cancelButton, sendButton);
            dialog.Add(new Label(1, 1, "Encryption Key (16 bytes hex):"), keyTextField,
                      new Label(1, 5, "Example: '0102030405060708090A0B0C0D0E0F10'"));
            keyTextField.SetFocus();

            Application.Run(dialog);
        }

        private void SendBiometricReadCommand()
        {
            if (!_controller.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var readerNumberTextField = new TextField(25, 1, 25, "0");
            var typeTextField = new TextField(25, 3, 25, "1");
            var formatTextField = new TextField(25, 5, 25, "0");
            var qualityTextField = new TextField(25, 7, 25, "1");

            void SendBiometricReadButtonClicked()
            {
                if (!byte.TryParse(readerNumberTextField.Text.ToString(), out var readerNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reader number entered!", "OK");
                    return;
                }

                if (!byte.TryParse(typeTextField.Text.ToString(), out var type))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid type entered!", "OK");
                    return;
                }

                if (!byte.TryParse(formatTextField.Text.ToString(), out var format))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid format entered!", "OK");
                    return;
                }

                if (!byte.TryParse(qualityTextField.Text.ToString(), out var quality))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid quality entered!", "OK");
                    return;
                }

                Application.RequestStop();

                ShowDeviceSelectionDialog("Biometric Read", async (address) =>
                {
                    try
                    {
                        await _controller.SendBiometricRead(address, readerNumber, type, format, quality);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.ErrorQuery(60, 10, "Error", ex.Message, "OK");
                    }
                });
            }

            var sendButton = new Button("Send", true);
            sendButton.Clicked += SendBiometricReadButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Biometric Read", 60, 15, cancelButton, sendButton);
            dialog.Add(new Label(1, 1, "Reader Number:"), readerNumberTextField,
                      new Label(1, 3, "Type:"), typeTextField,
                      new Label(1, 5, "Format:"), formatTextField,
                      new Label(1, 7, "Quality:"), qualityTextField);
            readerNumberTextField.SetFocus();

            Application.Run(dialog);
        }

        private void SendBiometricMatchCommand()
        {
            if (!_controller.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var readerNumberTextField = new TextField(25, 1, 25, "0");
            var typeTextField = new TextField(25, 3, 25, "1");
            var formatTextField = new TextField(25, 5, 25, "0");
            var qualityThresholdTextField = new TextField(25, 7, 25, "1");
            var templateDataTextField = new TextField(25, 9, 40, "");

            void SendBiometricMatchButtonClicked()
            {
                if (!byte.TryParse(readerNumberTextField.Text.ToString(), out var readerNumber))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid reader number entered!", "OK");
                    return;
                }

                if (!byte.TryParse(typeTextField.Text.ToString(), out var type))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid type entered!", "OK");
                    return;
                }

                if (!byte.TryParse(formatTextField.Text.ToString(), out var format))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid format entered!", "OK");
                    return;
                }

                if (!byte.TryParse(qualityThresholdTextField.Text.ToString(), out var qualityThreshold))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid quality threshold entered!", "OK");
                    return;
                }

                byte[] templateData;
                try
                {
                    var templateDataStr = templateDataTextField.Text.ToString();
                    if (string.IsNullOrWhiteSpace(templateDataStr))
                    {
                        MessageBox.ErrorQuery(40, 10, "Error", "Please enter template data!", "OK");
                        return;
                    }
                    templateData = Convert.FromHexString(templateDataStr);
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid template data hex format!", "OK");
                    return;
                }

                Application.RequestStop();

                ShowDeviceSelectionDialog("Biometric Match", async (address) =>
                {
                    try
                    {
                        await _controller.SendBiometricMatch(address, readerNumber, type, format, qualityThreshold, templateData);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.ErrorQuery(60, 10, "Error", ex.Message, "OK");
                    }
                });
            }

            var sendButton = new Button("Send", true);
            sendButton.Clicked += SendBiometricMatchButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("Biometric Match", 70, 17, cancelButton, sendButton);
            dialog.Add(new Label(1, 1, "Reader Number:"), readerNumberTextField,
                      new Label(1, 3, "Type:"), typeTextField,
                      new Label(1, 5, "Format:"), formatTextField,
                      new Label(1, 7, "Quality Threshold:"), qualityThresholdTextField,
                      new Label(1, 9, "Template Data (hex):"), templateDataTextField,
                      new Label(1, 11, "Example: '010203040506070809'"));
            readerNumberTextField.SetFocus();

            Application.Run(dialog);
        }

        private void SendFileTransferCommand()
        {
            if (!_controller.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            var typeTextField = new TextField(25, 1, 25, "1");
            var messageSizeTextField = new TextField(25, 3, 25, "128");
            var filePathTextField = new TextField(25, 5, 40, "");

            void SendFileTransferButtonClicked()
            {
                if (!byte.TryParse(typeTextField.Text.ToString(), out var type))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid type entered!", "OK");
                    return;
                }

                if (!byte.TryParse(messageSizeTextField.Text.ToString(), out var messageSize))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid message size entered!", "OK");
                    return;
                }

                var filePath = filePathTextField.Text.ToString();
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Please enter file path!", "OK");
                    return;
                }

                byte[] fileData;
                try
                {
                    if (!File.Exists(filePath))
                    {
                        MessageBox.ErrorQuery(40, 10, "Error", "File does not exist!", "OK");
                        return;
                    }
                    fileData = File.ReadAllBytes(filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(60, 10, "Error", $"Failed to read file: {ex.Message}", "OK");
                    return;
                }

                Application.RequestStop();

                ShowDeviceSelectionDialog("File Transfer", async (address) =>
                {
                    try
                    {
                        var totalFragments = await _controller.SendFileTransfer(address, type, fileData, messageSize);
                        MessageBox.Query(60, 10, "File Transfer Complete", 
                            $"File transferred successfully in {totalFragments} fragments.", "OK");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.ErrorQuery(60, 10, "Error", ex.Message, "OK");
                    }
                });
            }

            void BrowseFileButtonClicked()
            {
                var openDialog = new OpenDialog("Select File to Transfer", "", new List<string>());
                Application.Run(openDialog);

                if (!openDialog.Canceled && !string.IsNullOrEmpty(openDialog.FilePath?.ToString()))
                {
                    filePathTextField.Text = openDialog.FilePath.ToString();
                }
            }

            var sendButton = new Button("Send", true);
            sendButton.Clicked += SendFileTransferButtonClicked;
            var browseButton = new Button("Browse");
            browseButton.Clicked += BrowseFileButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog("File Transfer", 80, 15, cancelButton, sendButton);
            dialog.Add(new Label(1, 1, "Type:"), typeTextField,
                      new Label(1, 3, "Message Size:"), messageSizeTextField,
                      new Label(1, 5, "File Path:"), filePathTextField);
            
            browseButton.X = Pos.Right(filePathTextField) + 2;
            browseButton.Y = 5;
            dialog.Add(browseButton);
            
            typeTextField.SetFocus();

            Application.Run(dialog);
        }

        private void SendCustomCommand(string title, CommandData commandData)
        {
            if (!_controller.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            ShowDeviceSelectionDialog(title, async (address) =>
            {
                try
                {
                    await _controller.SendCustomCommand(address, commandData);
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
            if (!_controller.IsConnected)
            {
                MessageBox.ErrorQuery(60, 10, "Warning", "Start a connection before sending commands.", "OK");
            }
            else if (_controller.Settings.Devices.Count == 0)
            {
                MessageBox.ErrorQuery(60, 10, "Warning", "Add a device before sending commands.", "OK");
            }
        }

        private void ShowDeviceSelectionDialog(string title, Func<byte, Task> actionFunction)
        {
            var deviceList = _controller.GetDeviceList();
            var scrollView = new ScrollView(new Rect(6, 1, 50, 6))
            {
                ContentSize = new Size(40, deviceList.Length * 2),
                ShowVerticalScrollIndicator = deviceList.Length > 6,
                ShowHorizontalScrollIndicator = false
            };

            var deviceRadioGroup = new RadioGroup(0, 0, deviceList.Select(ustring.Make).ToArray())
            {
                SelectedItem = 0
            };
            scrollView.Add(deviceRadioGroup);

            async void SendCommandButtonClicked()
            {
                var selectedDevice = _controller.Settings.Devices.OrderBy(device => device.Address).ToArray()[deviceRadioGroup.SelectedItem];
                Application.RequestStop();
                await actionFunction(selectedDevice.Address);
            }

            var sendButton = new Button("Send", true);
            sendButton.Clicked += SendCommandButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += () => Application.RequestStop();

            var dialog = new Dialog(title, 60, 13, cancelButton, sendButton);
            dialog.Add(scrollView);
            sendButton.SetFocus();
            Application.Run(dialog);
        }

        private ComboBox CreatePortNameComboBox(int x, int y)
        {
            var portNames = SerialPort.GetPortNames();
            var portNameComboBox = new ComboBox(new Rect(x, y, 35, 5), portNames);

            // Select default port name
            if (portNames.Length > 0)
            {
                portNameComboBox.SelectedItem = Math.Max(
                    Array.FindIndex(portNames, port => 
                        string.Equals(port, _controller.Settings.SerialConnectionSettings.PortName)), 0);
            }

            return portNameComboBox;
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
            // Connection status updates are handled through messages
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

                _scrollView.Frame = new Rect(1, 0, _window.Frame.Width - 3, _window.Frame.Height - 2);
                _scrollView.RemoveAll();

                int index = 0;
                foreach (var message in _controller.MessageHistory.Reverse())
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

        public void Shutdown()
        {
            Application.Shutdown();
        }
    }
}