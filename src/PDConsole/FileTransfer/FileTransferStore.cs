using System;
using System.IO;

namespace PDConsole.FileTransfer
{
    /// <summary>
    /// Writes files received from the ACU through osdp_FILETRANSFER to disk, one file per completed
    /// transfer. Modeled on <see cref="Tracing.PDPacketCaptureTracer"/>'s capture directory handling.
    /// </summary>
    internal sealed class FileTransferStore
    {
        private const string FileTransferDirectory = "file-transfer";
        private const string FilePrefix = "file-transfer";

        private readonly string _fileTransferDirectoryPath;

        public FileTransferStore()
        {
            _fileTransferDirectoryPath = FileTransferDirectory;
        }

        /// <summary>
        /// Gets the directory that received files are written to.
        /// </summary>
        public string DirectoryPath => _fileTransferDirectoryPath;

        /// <summary>
        /// Writes a completed transfer to a new file.
        /// </summary>
        /// <param name="fileType">The osdp_FILETRANSFER type declared by the ACU.</param>
        /// <param name="contents">The assembled file contents.</param>
        /// <returns>The full path of the file written.</returns>
        public string Save(byte fileType, byte[] contents)
        {
            if (contents == null) throw new ArgumentNullException(nameof(contents));

            // Created on demand rather than in the constructor so a PD that never receives a file
            // does not leave an empty directory behind.
            if (!Directory.Exists(_fileTransferDirectoryPath))
            {
                Directory.CreateDirectory(_fileTransferDirectoryPath);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var path = Path.Combine(_fileTransferDirectoryPath, $"{FilePrefix}-{timestamp}-type{fileType:X2}.bin");

            // A second transfer completing within the same second would otherwise overwrite the first.
            path = EnsureUniquePath(path);

            File.WriteAllBytes(path, contents);

            return Path.GetFullPath(path);
        }

        private static string EnsureUniquePath(string path)
        {
            if (!File.Exists(path)) return path;

            var directory = Path.GetDirectoryName(path) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);

            for (int suffix = 2; ; suffix++)
            {
                var candidate = Path.Combine(directory, $"{name}-{suffix}{extension}");
                if (!File.Exists(candidate)) return candidate;
            }
        }
    }
}
