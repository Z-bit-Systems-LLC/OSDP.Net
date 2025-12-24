using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PassiveOsdpMonitor;
using PassiveOsdpMonitor.Configuration;
using System.Reflection;

// Get the directory where the executable is located (not the extraction folder)
var exeDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

var config = new ConfigurationBuilder()
    .SetBasePath(exeDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .Build();

var monitorConfig = new MonitorConfiguration();
config.GetSection("PassiveOsdpMonitor").Bind(monitorConfig);

// Check if config file was loaded or using defaults
var configSection = config.GetSection("PassiveOsdpMonitor");
bool usingDefaults = !configSection.Exists();

if (usingDefaults)
{
    Console.WriteLine("Note: appsettings.json not found, using default configuration");
    Console.WriteLine("Create appsettings.json to customize settings.");
    Console.WriteLine();
}

// Validate configuration
if (string.IsNullOrWhiteSpace(monitorConfig.SerialPort))
{
    Console.WriteLine("Error: SerialPort must be specified");
    return;
}

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<Program>();

logger.LogInformation("Passive OSDP Monitor v1.0");
logger.LogInformation("========================");
logger.LogInformation("Serial Port: {Port}", monitorConfig.SerialPort);
logger.LogInformation("Baud Rate: {BaudRate}", monitorConfig.BaudRate);

// Display security key information
if (monitorConfig.SecurityKey != null && monitorConfig.SecurityKey.Length > 0)
{
    logger.LogInformation("Security Key: Custom key ({Length} bytes)", monitorConfig.SecurityKey.Length);
}
else
{
    logger.LogInformation("Security Key: Using default OSDP key (SCBK-D)");
    logger.LogInformation("Note: Specify SecurityKey in appsettings.json for custom keys");
}

logger.LogInformation("OSDPCap File: {OsdpCapPath}", monitorConfig.OsdpCapFilePath);
logger.LogInformation("Parsed Text: {ParsedTextPath}", monitorConfig.ParsedTextFilePath);
logger.LogInformation("");
logger.LogInformation("Monitoring... Press Ctrl+C to stop");
logger.LogInformation("");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    logger.LogInformation("");
    logger.LogInformation("Stopping monitor...");
    cts.Cancel();
};

try
{
    var monitor = new PassiveMonitor(monitorConfig, logger);
    await monitor.StartAsync(cts.Token);
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error");
}

logger.LogInformation("Monitor stopped");
