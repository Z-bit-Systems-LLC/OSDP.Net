namespace PassiveOsdpMonitor.Configuration;

public class MonitorConfiguration
{
    public string SerialPort { get; set; } = "COM3";
    public int BaudRate { get; set; } = 9600;
    public string OutputPath { get; set; } = "./captures";
    public string OutputFilePrefix { get; set; } = "passive-capture";
    public byte[]? SecurityKey { get; set; } = null;

    private string GenerateBaseFilePath()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string filename = $"{OutputFilePrefix}-{timestamp}";
        return Path.Combine(OutputPath, filename);
    }

    public string OsdpCapFilePath => GenerateBaseFilePath() + ".osdpcap";
    public string ParsedTextFilePath => GenerateBaseFilePath() + ".txt";
}
