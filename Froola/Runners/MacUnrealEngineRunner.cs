using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Froola.Interfaces;

namespace Froola.Runners;

/// <summary>
/// Mac implementation of Unreal Engine runner for remote build, test, and file operations via SSH.
/// </summary>
public class MacUnrealEngineRunner(ISshConnection sshConnection) : IMacUnrealEngineRunner, IDisposable
{
    private bool _disposed;
    
    #region Directory Operations

    /// <summary>
    /// Creates a directory on the remote Mac.
    /// </summary>
    public async Task<bool> MakeDirectory(string path)
    {
        return await sshConnection.EnsureDirectoryExists(path);
    }

    /// <summary>
    ///     Copies a directory from source to destination on the remote Mac.
    /// </summary>
    public async Task<bool> CopyDirectory(string sourcePath, string destinationPath)
    {
        try
        {
            // Check if source directory exists
            if (!await DirectoryExists(sourcePath))
            {
                return false;
            }

            if (!await sshConnection.EnsureDirectoryExists(destinationPath))
            {
                await sshConnection.MakeDirectory(destinationPath);
            }

            // Use cp command with recursive flag to copy the directory
            var command = $"cp -R '{sourcePath}' '{destinationPath}'";
            var result = await sshConnection.SendCommand(command);

            // Verify that the copy was successful by checking if destination exists
            return await DirectoryExists(destinationPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a directory exists on the remote Mac.
    /// </summary>
    public async Task<bool> DirectoryExists(string path)
    {
        var result = await sshConnection.SendCommand($"test -d '{path}' && echo exists || echo notfound");
        return result.Trim() == "exists";
    }

    /// <summary>
    /// Deletes a directory on the remote Mac.
    /// </summary>
    public async Task<bool> DeleteDirectory(string remotePath)
    {
        try
        {
            // Return the result of sshConnection.DeleteDirectory directly
            return await sshConnection.DeleteDirectory(remotePath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Downloads a directory from the remote Mac to the local machine.
    /// </summary>
    public async Task<bool> DownloadDirectory(string remotePath, string localPath)
    {
        if (!await sshConnection.EnsureDirectoryExists(remotePath))
        {
            return false;
        }

        try
        {
            await sshConnection.DownloadDirectory(remotePath, localPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Uploads a directory from the local machine to the remote Mac.
    /// </summary>
    public async Task<bool> UploadDirectory(string localPath, string remotePath)
    {
        try
        {
            await sshConnection.UploadDirectory(localPath, remotePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region File Operations

    /// <summary>
    /// Checks if a file exists on the remote Mac.
    /// </summary>
    public async Task<bool> FileExists(string path)
    {
        try
        {
            return await sshConnection.FileExists(path);
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Xcode Operations

    /// <summary>
    /// Gets the current Xcode path on the remote Mac.
    /// </summary>
    public async Task<string> GetCurrentXcodePath()
    {
        var fullPath = (await sshConnection.SendCommand("xcode-select -p")).Trim();
        // Remove /Contents/Developer if present
        const string suffix = "/Contents/Developer";
        return fullPath.EndsWith(suffix, StringComparison.Ordinal)
            ? fullPath[..^suffix.Length]
            : fullPath;
    }

    /// <summary>
    /// Switches the active Xcode to the specified path and returns the previous Xcode path.
    /// </summary>
    /// <param name="xcodePath">Xcode path to switch to.</param>
    /// <returns>Previous Xcode path.</returns>
    public async Task<string> SwitchXcode(string xcodePath)
    {
        // Get current Xcode path
        var originalXcodePath = await GetCurrentXcodePath();
        // Switch to the specified Xcode
        await sshConnection.SendCommand($"sudo xcode-select --switch '{xcodePath}'");
        return originalXcodePath;
    }

    #endregion
    

    /// <summary>
    /// Executes a remote script and yields log lines as they are produced. Returns the exit code at the end.
    /// </summary>
    /// <param name="commands">Commands to execute.</param>
    /// <param name="envMap">Environment variables to set.</param>
    /// <returns>Log lines (yield return) and exit code (return).</returns>
    public async IAsyncEnumerable<string> ExecuteRemoteScriptWithLogsAsync(IEnumerable<string> commands,
        Dictionary<string, string>? envMap = null)
    {
        List<string> inputs = [];
        if (envMap is not null)
        {
            inputs.AddRange(envMap.Select(envValuePair => $"export {envValuePair.Key}={envValuePair.Value}"));
        }

        inputs.AddRange(commands);

        await foreach (var logLine in sshConnection.RunCommandWithSshShellAsyncEnumerable(inputs))
        {
            yield return logLine;
        }
    }

    /// <summary>
    /// Executes a remote script and yields log lines as they are produced. Returns the exit code at the end.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="envMap">Environment variables to set.</param>
    /// <returns>Log lines (yield return) and exit code (return).</returns>
    public async IAsyncEnumerable<string> ExecuteRemoteScriptWithLogsAsync(string command,
        Dictionary<string, string>? envMap = null)
    {
        List<string> inputs = [];
        if (envMap is not null)
        {
            inputs.AddRange(envMap.Select(envValuePair => $"export {envValuePair.Key}={envValuePair.Value}"));
        }

        inputs.Add(command);

        await foreach (var logLine in sshConnection.RunCommandWithSshShellAsyncEnumerable(inputs))
        {
            yield return logLine;
        }
    }

    /// <summary>
    /// Disposes the SSH connection and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        sshConnection.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}