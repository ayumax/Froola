using System.Collections.Generic;
using System.Threading.Tasks;

namespace Froola.Interfaces
{
    /// <summary>
    ///     Interface for SSH and SCP connection wrapper.
    /// </summary>
    public interface ISshConnection : System.IDisposable
    {
        Task<string> SendCommand(string commandString);
        IAsyncEnumerable<string> RunCommandWithSshShellAsyncEnumerable(IEnumerable<string> inputs);
        Task UploadDirectory(string localPath, string remotePath);
        Task UploadFile(string localFilePath, string remoteFilePath);
        Task DownloadDirectory(string remotePath, string localPath);
        Task DownloadFile(string remoteFilePath, string localFilePath);
        Task<string> MakeDirectory(string remotePath);
        Task<string> MakeDirectoryWithParents(string remotePath, bool createParents);
        Task<bool> EnsureDirectoryExists(string remotePath);
        Task<bool> FileExists(string remoteFilePath);
        Task<bool> CreateTextFile(string remoteFilePath, string content);
        Task<bool> DeleteDirectory(string remotePath);
        Task MakeExecutable(string remotePath);
        Task<string> GetFileContent(string filePath);
    }
}
