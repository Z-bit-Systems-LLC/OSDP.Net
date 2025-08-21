using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ACUConsole.Configuration;
using ACUConsole.Dialogs;
using ACUConsole.Model;
using OSDP.Net.Model.CommandData;
using NStack;
using Terminal.Gui;

namespace ACUConsole
{
    /// <summary>
    /// View class that handles all Terminal.Gui UI elements and interactions for ACU Console
    /// </summary>
    public class ACUConsoleView : IACUConsoleView
    {
        private readonly IACUConsolePresenter _presenter;
        
        // UI Components
        private Window _window;
        private ScrollView _scrollView;
        private MenuBar _menuBar;
        private readonly MenuItem _discoverMenuItem;
        
        public ACUConsoleView(IACUConsolePresenter presenter)
        {
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            
            // Create discover menu item that can be updated
            _discoverMenuItem = new MenuItem("_Discover", string.Empty, DiscoverDevice);
            
            // Subscribe to presenter events
            _presenter.MessageReceived += OnMessageReceived;
            _presenter.StatusChanged += OnStatusChanged;
            _presenter.ConnectionStatusChanged += OnConnectionStatusChanged;
            _presenter.ErrorOccurred += OnErrorOccurred;
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
                    new MenuItem("_Save Configuration", "", () => _presenter.SaveConfiguration()),
                    new MenuItem("_Quit", "", Quit)
                }),
                new MenuBarItem("Co_nnections", new[]
                {
                    new MenuItem("Start Serial Connection", "", StartSerialConnection),
                    new MenuItem("Start TCP Server Connection", "", StartTcpServerConnection),
                    new MenuItem("Start TCP Client Connection", "", StartTcpClientConnection),
                    new MenuItem("Stop Connections", "", () => _ = _presenter.StopConnection())
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
                    new MenuItem("_Device Capabilities", "", () => SendSimpleCommand("Device capabilities", _presenter.SendDeviceCapabilities)),
                    new MenuItem("Encryption Key Set", "", SendEncryptionKeySetCommand),
                    new MenuItem("File Transfer", "", SendFileTransferCommand),
                    new MenuItem("_ID Report", "", () => SendSimpleCommand("ID report", _presenter.SendIdReport)),
                    new MenuItem("Input Status", "", () => SendSimpleCommand("Input status", _presenter.SendInputStatus)),
                    new MenuItem("_Local Status", "", () => SendSimpleCommand("Local Status", _presenter.SendLocalStatus)),
                    new MenuItem("Manufacturer Specific", "", SendManufacturerSpecificCommand),
                    new MenuItem("Output Control", "", SendOutputControlCommand),
                    new MenuItem("Output Status", "", () => SendSimpleCommand("Output status", _presenter.SendOutputStatus)),
                    new MenuItem("Reader Buzzer Control", "", SendReaderBuzzerControlCommand),
                    new MenuItem("Reader LED Control", "", SendReaderLedControlCommand),
                    new MenuItem("Reader Text Output", "", SendReaderTextOutputCommand),
                    new MenuItem("_Reader Status", "", () => SendSimpleCommand("Reader status", _presenter.SendReaderStatus))
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
                    _presenter.SaveConfiguration();
                    Application.Shutdown();
                    break;
            }
        }

        // Connection Methods - Using extracted dialog classes
        private async void StartSerialConnection()
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

        private async void StartTcpServerConnection()
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

        private async void StartTcpClientConnection()
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
                    _presenter.ParseOSDPCapFile(filePath, address, ignorePollsAndAcksCheckBox.Checked, key);
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
                    _presenter.LoadConfiguration();
                    MessageBox.Query(40, 6, "Load Configuration", "Load completed successfully", "OK");
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
                }
                catch (Exception ex)
                {
                    ShowError("Error", ex.Message);
                }
            }
        }

        private void DiscoverDevice()
        {
            var portNames = SerialPort.GetPortNames();
            var portNameComboBox = new ComboBox(new Rect(15, 1, 35, 5), portNames);
            
            // Select default port name
            if (portNames.Length > 0)
            {
                portNameComboBox.SelectedItem = Math.Max(
                    Array.FindIndex(portNames, port => 
                        string.Equals(port, _presenter.Settings.SerialConnectionSettings.PortName)), 0);
            }
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

                    await _presenter.DiscoverDevice(portNameComboBox.Text.ToString(), pingTimeout, reconnectDelay, cancellationTokenSource.Token);
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
            if (!_presenter.CanSendCommand())
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

        private async void SendCommunicationConfiguration()
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

        private async void SendOutputControlCommand()
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

        private async void SendReaderLedControlCommand()
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

        private async void SendReaderBuzzerControlCommand()
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

        private async void SendReaderTextOutputCommand()
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

        private async void SendManufacturerSpecificCommand()
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

        private void SendEncryptionKeySetCommand()
        {
            if (!_presenter.CanSendCommand())
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
                        await _presenter.SendEncryptionKeySet(address, key);
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

        private async void SendBiometricReadCommand()
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

        private void SendBiometricMatchCommand()
        {
            if (!_presenter.CanSendCommand())
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
                        await _presenter.SendBiometricMatch(address, readerNumber, type, format, qualityThreshold, templateData);
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
            if (!_presenter.CanSendCommand())
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
                        var totalFragments = await _presenter.SendFileTransfer(address, type, fileData, messageSize);
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
            if (!_presenter.CanSendCommand())
            {
                ShowCommandRequirementsError();
                return;
            }

            ShowDeviceSelectionDialog(title, async (address) =>
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

        private void ShowDeviceSelectionDialog(string title, Func<byte, Task> actionFunction)
        {
            var deviceList = _presenter.GetDeviceList();
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
                var selectedDevice = _presenter.Settings.Devices.OrderBy(device => device.Address).ToArray()[deviceRadioGroup.SelectedItem];
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