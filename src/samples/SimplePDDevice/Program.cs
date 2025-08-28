using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OSDP.Net;
using OSDP.Net.Connections;

namespace SimplePDDevice;

/// <summary>
/// Simple console application that demonstrates a basic OSDP Peripheral Device
/// </summary>
internal class Program
{
    private static async Task Main()
    {
        Console.WriteLine("Simple OSDP Peripheral Device");
        Console.WriteLine("============================");

        // Load configuration
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();
        
        var osdpSection = config.GetSection("OSDP");
        
        // Configuration with defaults
        int tcpPort = int.Parse(osdpSection["TcpPort"] ?? "4900");
        byte deviceAddress = byte.Parse(osdpSection["DeviceAddress"] ?? "1");
        bool requireSecurity = bool.Parse(osdpSection["RequireSecurity"] ?? "false");
        var securityKey = System.Text.Encoding.ASCII.GetBytes(osdpSection["SecurityKey"] ?? "0011223344556677889900AABBCCDDEEFF");

        // Setup logging
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information)
                .AddFilter("SimplePDDevice", LogLevel.Information)
                .AddFilter("OSDP.Net", LogLevel.Warning); // Reduce OSDP.Net noise
        });

        var logger = loggerFactory.CreateLogger<Program>();

        // Device configuration
        var deviceConfiguration = new DeviceConfiguration
        {
            Address = deviceAddress,
            RequireSecurity = requireSecurity,
            SecurityKey = securityKey
        };

        // Setup TCP connection listener
        var connectionListener = new TcpConnectionListener(tcpPort, 9600);

        // Create and start the device
        using var device = new SimplePDDevice(deviceConfiguration);
        
        logger.LogInformation("Starting OSDP Peripheral Device on TCP port {Port}", tcpPort);
        logger.LogInformation("Device Address: {Address}", deviceAddress);
        logger.LogInformation("Security Required: {RequireSecurity}", requireSecurity);
        
        device.StartListening(connectionListener);

        logger.LogInformation("Device is now listening for ACU connections...");
        logger.LogInformation("Press 'q' to quit");

        // Simple console loop - check for 'q' or run for 30 seconds, then exit
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000, cts.Token);

                Console.WriteLine(device.IsConnected
                    ? $"[{DateTime.Now:HH:mm:ss}] Device is connected to ACU"
                    : $"[{DateTime.Now:HH:mm:ss}] Waiting for ACU connection...");
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when timeout occurs
        }

        logger.LogInformation("Shutting down device...");
        await device.StopListening();
        logger.LogInformation("Device stopped");
    }
}