using System.Collections.Generic;
using System.Threading.Tasks;

namespace Froola.Interfaces;

/// <summary>
/// Interface for running Unreal Engine operations on Linux via SSH.
/// </summary>
public interface ILinuxUnrealEngineRunner
{
    #region Directory Operations
    Task<bool> MakeDirectory(string path);
    Task<bool> CopyDirectory(string sourcePath, string destinationPath);
    Task<bool> DirectoryExists(string path);
    Task<bool> DeleteDirectory(string remotePath);
    Task<bool> DownloadDirectory(string remotePath, string localPath);
    Task<bool> UploadDirectory(string localPath, string remotePath);
    #endregion

    #region File Operations
    Task<bool> FileExists(string path);
    #endregion

    #region Miscellaneous
    IAsyncEnumerable<string> ExecuteRemoteScriptWithLogsAsync(IEnumerable<string> commands,
        Dictionary<string, string>? envMap = null);

    IAsyncEnumerable<string> ExecuteRemoteScriptWithLogsAsync(string command,
        Dictionary<string, string>? envMap = null);
    #endregion
}
