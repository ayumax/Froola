using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Froola.Interfaces;

namespace Froola.Runners;

/// <summary>
/// Linux implementation of Unreal Engine runner for remote build, test, and file operations via SSH.
/// </summary>
public class LinuxUnrealEngineRunner(ILinuxSshConnection sshConnection) : ILinuxUnrealEngineRunner, IDisposable
{
    private bool _disposed;

    #region Directory Operations

    public async Task<bool> MakeDirectory(string path)
    {
        return await sshConnection.EnsureDirectoryExists(path);
    }

    public async Task<bool> CopyDirectory(string sourcePath, string destinationPath)
    {
        try
        {
            if (!await DirectoryExists(sourcePath))
            {
                return false;
            }

            if (!await sshConnection.EnsureDirectoryExists(destinationPath))
            {
                await sshConnection.MakeDirectory(destinationPath);
            }

            var command = $"cp -R '{sourcePath}' '{destinationPath}'";
            await sshConnection.SendCommand(command);

            return await DirectoryExists(destinationPath);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DirectoryExists(string path)
    {
        var result = await sshConnection.SendCommand($"test -d '{path}' && echo exists || echo notfound");
        return result.Trim() == "exists";
    }

    public async Task<bool> DeleteDirectory(string remotePath)
    {
        try
        {
            return await sshConnection.DeleteDirectory(remotePath);
        }
        catch
        {
            return false;
        }
    }

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

    #region Miscellaneous

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

    #endregion

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
