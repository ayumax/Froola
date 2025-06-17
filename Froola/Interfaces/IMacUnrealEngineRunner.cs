using System.Collections.Generic;
using System.Threading.Tasks;

namespace Froola.Interfaces;

/// <summary>
/// Interface for running Unreal Engine operations on Mac via SSH.
/// </summary>
public interface IMacUnrealEngineRunner
{
    #region Directory Operations
    /// <summary>
    /// Creates a directory on the remote Mac.
    /// </summary>
    Task<bool> MakeDirectory(string path);

    /// <summary>
    ///     Copy directory
    /// </summary>
    /// <param name="sourcePath">source directory path</param>
    /// <param name="destinationPath">destination directory path</param>
    /// <returns></returns>
    Task<bool> CopyDirectory(string sourcePath, string destinationPath);

    /// <summary>
    /// Checks if a directory exists on the remote Mac.
    /// </summary>
    Task<bool> DirectoryExists(string path);

    /// <summary>
    /// Deletes a directory on the remote Mac.
    /// </summary>
    Task<bool> DeleteDirectory(string remotePath);

    /// <summary>
    /// Downloads a directory from the remote Mac to the local machine.
    /// </summary>
    Task<bool> DownloadDirectory(string remotePath, string localPath);

    /// <summary>
    /// Uploads a directory from the local machine to the remote Mac.
    /// </summary>
    Task<bool> UploadDirectory(string localPath, string remotePath);
    #endregion

    #region File Operations
    /// <summary>
    /// Checks if a file exists on the remote Mac.
    /// </summary>
    Task<bool> FileExists(string path);
    
    #endregion

    #region Xcode Operations
    /// <summary>
    /// Gets the current Xcode path on the remote Mac.
    /// </summary>
    Task<string> GetCurrentXcodePath();

    /// <summary>
    /// Switches the active Xcode to the specified path and returns the previous Xcode path.
    /// </summary>
    /// <param name="xcodePath">Xcode path to switch to.</param>
    /// <returns>Previous Xcode path.</returns>
    Task<string> SwitchXcode(string xcodePath);
    #endregion

    

    #region Miscellaneous


    /// <summary>
    /// Executes a remote script and yields log lines as they are produced. Returns the exit code at the end.
    /// </summary>
    /// <param name="commands">Parameters for script execution.</param>
    /// <param name="envMap">Environment variables for the remote command.</param>
    /// <returns>Log lines (yield return) and exit code (return).</returns>
    IAsyncEnumerable<string> ExecuteRemoteScriptWithLogsAsync(IEnumerable<string> commands,
        Dictionary<string, string>? envMap = null);

    /// <summary>
    /// Executes a remote script and yields log lines as they are produced. Returns the exit code at the end.
    /// </summary>
    /// <param name="command">Command to execute remotely.</param>
    /// <param name="envMap">Environment variables for the remote command.</param>
    /// <returns>Log lines from the remote script.</returns>
    IAsyncEnumerable<string> ExecuteRemoteScriptWithLogsAsync(string command,
        Dictionary<string, string>? envMap = null);

    #endregion
}