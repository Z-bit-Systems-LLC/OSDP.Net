using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PassiveOsdpMonitor;
using PassiveOsdpMonitor.Configuration;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var monitorConfig = new MonitorConfiguration();
config.GetSection("PassiveOsdpMonitor").Bind(monitorConfig);

// Validate configuration
if (string.IsNullOrWhiteSpace(monitorConfig.SerialPort))
{
    Console.WriteLine("Error: SerialPort must be specified in appsettings.json");
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
