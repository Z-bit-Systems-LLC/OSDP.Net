using System.IO;

namespace OSDP.Net.Tracing;

/// <summary>
/// Abstraction for file system operations to enable testing without actual file I/O.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Creates all directories in the specified path.
    /// </summary>
    /// <param name="path">The directory path to create.</param>
    void CreateDirectory(string path);

    /// <summary>
    /// Creates a StreamWriter for the specified file path.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="append">If true, appends to the file; otherwise creates new file.</param>
    /// <returns>A StreamWriter instance.</returns>
    StreamWriter CreateStreamWriter(string path, bool append);

    /// <summary>
    /// Gets the directory name from a file path.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The directory name.</returns>
    string GetDirectoryName(string path);
}