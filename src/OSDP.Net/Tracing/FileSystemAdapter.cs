using System.IO;

namespace OSDP.Net.Tracing;

/// <summary>
/// Default implementation of IFileSystem that uses actual file system operations.
/// </summary>
internal class FileSystemAdapter : IFileSystem
{
    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public StreamWriter CreateStreamWriter(string path, bool append)
    {
        return new StreamWriter(path, append);
    }

    public string GetDirectoryName(string path)
    {
        return Path.GetDirectoryName(path);
    }
}