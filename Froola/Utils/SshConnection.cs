using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Froola.Configs;
using Froola.Interfaces;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Froola.Utils;

/// <summary>
///     SSH and SCP connection wrapper to simplify remote operations.
/// </summary>
public sealed class SshConnection(IFroolaLogger<SshConnection> logger, IOptions<MacConfig> macOptions)
    : IDisposable, ISshConnection
{
    private bool _disposed;
    private ScpClient? _scpClient;
    private SshClient? _sshClient;

    /// <summary>
    ///     Disposes SSH and SCP clients.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Connects to the SSH server if not already connected.
    /// </summary>
    private void Connect()
    {
        if (_sshClient is { IsConnected: true })
        {
            return;
        }

        try
        {
            _sshClient = new SshClient(MakeConnectionInfo());
            _sshClient.Connect();

            if (!_sshClient.IsConnected)
            {
                throw new Exception("SSH connection failed");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to connect to SSH server: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Ensures SCP client is connected.
    /// </summary>
    private void ConnectScp()
    {
        if (_scpClient is { IsConnected: true })
        {
            return;
        }

        try
        {
            _scpClient = new ScpClient(MakeConnectionInfo());
            // Handle paths with spaces
            _scpClient.RemotePathTransformation = RemotePathTransformation.ShellQuote;
            _scpClient.Connect();

            if (!_scpClient.IsConnected)
            {
                throw new Exception("SCP connection failed");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to connect to SCP server: {ex.Message}", ex);
        }
    }

    private ConnectionInfo MakeConnectionInfo()
    {
        var commonConfig = macOptions.Value;

        AuthenticationMethod? authMethod = null;

        if (string.IsNullOrWhiteSpace(commonConfig.SshPassword))
        {
            var keyFile = new PrivateKeyFile(commonConfig.SshPrivateKeyPath);
            authMethod = new PrivateKeyAuthenticationMethod(commonConfig.SshUser, keyFile);
        }
        else
        {
            authMethod = new PasswordAuthenticationMethod(commonConfig.SshUser, commonConfig.SshPassword);
        }

        return new ConnectionInfo(commonConfig.SshHost, commonConfig.SshPort,
            commonConfig.SshUser, authMethod);
    }

    /// <summary>
    ///     Executes a command on the remote server.
    /// </summary>
    /// <param name="commandString">Command to execute.</param>
    /// <returns>Command output (stdout and stderr).</returns>
    public async Task<string> SendCommand(string commandString)
    {
        Connect();

        var cmd = _sshClient!.CreateCommand(commandString);
        await cmd.ExecuteAsync();

        var retString = "";
        if (!string.IsNullOrWhiteSpace(cmd.Result))
        {
            retString = cmd.Result;
        }

        if (!string.IsNullOrWhiteSpace(cmd.Error))
        {
            retString += "error: " + cmd.Error;
        }

        logger.LogInformation(retString);
        
        return retString;
    }

    /// <summary>
    ///     Runs a command on the remote server with SSH shell.
    /// </summary>
    /// <param name="inputs">Input strings to send to the remote server.</param>
    /// <returns>An asynchronous enumerable of output strings from the remote server.</returns>
    public async IAsyncEnumerable<string> RunCommandWithSshShellAsyncEnumerable(IEnumerable<string> inputs)
    {
        Connect();

        await using var stream = _sshClient!.CreateShellStreamNoTerminal();

        var isContinue = true;

        void OnClosed(object? sender, EventArgs args)
        {
            if (!isContinue)
            {
                return;
            }

            stream.WriteLine("connection closed");
            isContinue = false;
        }

        void OnErrorOccurred(object? sender, ExceptionEventArgs args)
        {
            if (!isContinue)
            {
                return;
            }

            stream.WriteLine(args.Exception.Message);
            isContinue = false;
        }

        stream.Closed += OnClosed;
        stream.ErrorOccurred += OnErrorOccurred;

        foreach (var input in inputs)
        {
            stream.WriteLine(input);
        }

        var endMarker = $"__Finished_{nameof(RunCommandWithSshShellAsyncEnumerable)}__{Guid.NewGuid()}";
        stream.WriteLine($"echo {endMarker}");

        while (isContinue)
        {
            while (stream.DataAvailable)
            {
                var line = stream.ReadLine();
                if (line is null)
                {
                    logger.LogError(
                        "RunCommandWithSshShellAsyncEnumerable is terminated because the stream was unexpectedly closed.");
                    isContinue = false;
                    yield break;
                }

                if (line == endMarker)
                {
                    isContinue = false;
                    yield break;
                }

                yield return line;
            }

            if (isContinue)
            {
                await Task.Delay(100);
            }
        }

        stream.Closed -= OnClosed;
        stream.ErrorOccurred -= OnErrorOccurred;
    }

    /// <summary>
    ///     Uploads a local directory to the remote server.
    /// </summary>
    /// <param name="localPath">Local directory path.</param>
    /// <param name="remotePath">Remote directory path.</param>
    public async Task UploadDirectory(string localPath, string remotePath)
    {
        await Task.Run(() =>
        {
            ConnectScp();
            var localDir = new DirectoryInfo(localPath);
            logger.LogInformation($"Uploading directory from {localDir.FullName} to {remotePath}");
            _scpClient!.Upload(localDir, remotePath);
        });
    }

    /// <summary>
    ///     Uploads a file to the remote server.
    /// </summary>
    /// <param name="localFilePath">Local file path.</param>
    /// <param name="remoteFilePath">Remote file path.</param>
    public async Task UploadFile(string localFilePath, string remoteFilePath)
    {
        await Task.Run(() =>
        {
            ConnectScp();
            logger.LogInformation($"Uploading file from {localFilePath} to {remoteFilePath}");
            _scpClient!.Upload(new FileInfo(localFilePath), remoteFilePath);
        });
    }

    /// <summary>
    ///     Downloads a directory from the remote server.
    /// </summary>
    /// <param name="remotePath">Remote directory path.</param>
    /// <param name="localPath">Local directory path.</param>
    public async Task DownloadDirectory(string remotePath, string localPath)
    {
        await Task.Run(() =>
        {
            ConnectScp();
            logger.LogInformation($"Downloading directory from {remotePath} to {localPath}");
            _scpClient!.Download(remotePath, new DirectoryInfo(localPath));
        });
    }

    /// <summary>
    ///     Downloads a file from the remote server.
    /// </summary>
    /// <param name="remoteFilePath">Remote file path.</param>
    /// <param name="localFilePath">Local file path.</param>
    public async Task DownloadFile(string remoteFilePath, string localFilePath)
    {
        await Task.Run(() =>
        {
            ConnectScp();
            logger.LogInformation($"Downloading file from {remoteFilePath} to {localFilePath}");
            _scpClient!.Download(remoteFilePath, new FileInfo(localFilePath));
        });
    }

    /// <summary>
    ///     Creates a directory on the remote server.
    /// </summary>
    /// <param name="remotePath">Remote directory path.</param>
    /// <returns>Command output.</returns>
    public async Task<string> MakeDirectory(string remotePath)
    {
        Connect();
        return await SendCommand($"mkdir -p \"{remotePath}\"");
    }

    /// <summary>
    ///     Creates a directory on the remote server with option to create parent directories.
    /// </summary>
    /// <param name="remotePath">Remote directory path.</param>
    /// <param name="createParents">Whether to create parent directories if they don't exist.</param>
    /// <returns>Command output.</returns>
    public async Task<string> MakeDirectoryWithParents(string remotePath, bool createParents = true)
    {
        Connect();
        var command = createParents
            ? $"mkdir -p \"{remotePath}\""
            : $"mkdir \"{remotePath}\"";
        return await SendCommand(command);
    }

    /// <summary>
    ///     Creates a directory on the remote server if it doesn't exist.
    /// </summary>
    /// <param name="remotePath">Remote directory path.</param>
    /// <returns>True if directory exists or was created.</returns>
    public async Task<bool> EnsureDirectoryExists(string remotePath)
    {
        Connect();

        // Create directory with -p flag to create parent directories if needed
        await SendCommand($"mkdir -p \"{remotePath}\"");

        // Verify directory exists
        var result = await SendCommand($"test -d \"{remotePath}\" && echo \"exists\" || echo \"not found\"");
        return result.Trim() == "exists";
    }

    /// <summary>
    ///     Checks if a file exists on the remote server.
    /// </summary>
    /// <param name="remoteFilePath">Remote file path.</param>
    /// <returns>True if file exists.</returns>
    public async Task<bool> FileExists(string remoteFilePath)
    {
        Connect();

        var result = await SendCommand($"test -f \"{remoteFilePath}\" && echo \"exists\" || echo \"not found\"");
        return result.Trim() == "exists";
    }

    /// <summary>
    ///     Creates a text file on the remote server with the specified content.
    /// </summary>
    /// <param name="remoteFilePath">Remote file path.</param>
    /// <param name="content">File content.</param>
    /// <returns>True if file was created successfully.</returns>
    public async Task<bool> CreateTextFile(string remoteFilePath, string content)
    {
        Connect();

        // Create file using cat and heredoc
        var createFileCmd = $"cat > \"{remoteFilePath}\" << 'EOL'\n{content}\nEOL";
        await SendCommand(createFileCmd);

        // Verify file exists
        return await FileExists(remoteFilePath);
    }

    /// <summary>
    ///     Deletes a directory on the remote server.
    /// </summary>
    /// <param name="remotePath">Remote directory path.</param>
    /// <returns>True if directory was deleted or doesn't exist.</returns>
    public async Task<bool> DeleteDirectory(string remotePath)
    {
        Connect();

        // Check if directory exists before attempting removal
        var dirExistsCheck =
            await SendCommand($"test -d \"{remotePath}\" && echo \"exists\" || echo \"not found\"");

        if (dirExistsCheck.Trim() == "exists")
            // Remove directory recursively
        {
            await SendCommand($"rm -rf \"{remotePath}\"");
        }

        // Verify directory is gone
        var afterCheck = await SendCommand($"test -d \"{remotePath}\" && echo \"exists\" || echo \"not found\"");
        return afterCheck.Trim() == "not found";
    }

    /// <summary>
    ///     Makes a file executable on the remote server.
    /// </summary>
    /// <param name="remotePath">Remote file path.</param>
    /// <returns>A task representing the operation.</returns>
    public Task MakeExecutable(string remotePath)
    {
        return SendCommand($"chmod +x '{remotePath}'");
    }

    /// <summary>
    ///     Retrieves the content of a file on the remote server.
    /// </summary>
    /// <param name="filePath">Remote file path.</param>
    /// <returns>The file content as a string.</returns>
    public async Task<string> GetFileContent(string filePath)
    {
        return await SendCommand($"cat '{filePath}' 2>/dev/null || echo 'No output file found'");
    }

    /// <summary>
    ///     Disposes managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose; false if called from finalizer.</param>
    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            if (_sshClient != null)
            {
                if (_sshClient.IsConnected)
                {
                    _sshClient.Disconnect();
                }

                _sshClient.Dispose();
                _sshClient = null;
            }

            if (_scpClient != null)
            {
                if (_scpClient.IsConnected)
                {
                    _scpClient.Disconnect();
                }

                _scpClient.Dispose();
                _scpClient = null;
            }
        }

        _disposed = true;
    }

    /// <summary>
    ///     Finalizer.
    /// </summary>
    ~SshConnection()
    {
        Dispose(false);
    }
}