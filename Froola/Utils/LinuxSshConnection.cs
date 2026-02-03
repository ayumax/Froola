using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Froola.Configs;
using Froola.Interfaces;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Froola.Utils;

/// <summary>
///     SSH and SCP connection wrapper for Linux operations.
/// </summary>
public sealed class LinuxSshConnection(IFroolaLogger<LinuxSshConnection> logger, LinuxConfig linuxConfig)
    : IDisposable, ILinuxSshConnection
{
    private bool _disposed;
    private ScpClient? _scpClient;
    private SshClient? _sshClient;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

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

    private void ConnectScp()
    {
        if (_scpClient is { IsConnected: true })
        {
            return;
        }

        try
        {
            _scpClient = new ScpClient(MakeConnectionInfo());
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
        AuthenticationMethod? authMethod = null;

        if (string.IsNullOrWhiteSpace(linuxConfig.SshPassword))
        {
            var keyFile = new PrivateKeyFile(linuxConfig.SshPrivateKeyPath);
            authMethod = new PrivateKeyAuthenticationMethod(linuxConfig.SshUser, keyFile);
        }
        else
        {
            authMethod = new PasswordAuthenticationMethod(linuxConfig.SshUser, linuxConfig.SshPassword);
        }

        return new ConnectionInfo(linuxConfig.SshHost, linuxConfig.SshPort, linuxConfig.SshUser, authMethod);
    }

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

    public async Task UploadFile(string localFilePath, string remoteFilePath)
    {
        await Task.Run(() =>
        {
            ConnectScp();
            logger.LogInformation($"Uploading file from {localFilePath} to {remoteFilePath}");
            _scpClient!.Upload(new FileInfo(localFilePath), remoteFilePath);
        });
    }

    public async Task DownloadDirectory(string remotePath, string localPath)
    {
        await Task.Run(() =>
        {
            ConnectScp();
            logger.LogInformation($"Downloading directory from {remotePath} to {localPath}");
            _scpClient!.Download(remotePath, new DirectoryInfo(localPath));
        });
    }

    public async Task DownloadFile(string remoteFilePath, string localFilePath)
    {
        await Task.Run(() =>
        {
            ConnectScp();
            logger.LogInformation($"Downloading file from {remoteFilePath} to {localFilePath}");
            _scpClient!.Download(remoteFilePath, new FileInfo(localFilePath));
        });
    }

    public async Task<string> MakeDirectory(string remotePath)
    {
        Connect();
        return await SendCommand($"mkdir -p \"{remotePath}\"");
    }

    public async Task<string> MakeDirectoryWithParents(string remotePath, bool createParents)
    {
        Connect();
        var command = createParents ? $"mkdir -p \"{remotePath}\"" : $"mkdir \"{remotePath}\"";
        return await SendCommand(command);
    }

    public async Task<bool> EnsureDirectoryExists(string remotePath)
    {
        Connect();
        await SendCommand($"mkdir -p \"{remotePath}\"");
        var result = await SendCommand($"test -d \"{remotePath}\" && echo \"exists\" || echo \"not found\"");
        return result.Trim() == "exists";
    }

    public async Task<bool> FileExists(string remoteFilePath)
    {
        Connect();
        var result = await SendCommand($"test -f \"{remoteFilePath}\" && echo \"exists\" || echo \"not found\"");
        return result.Trim() == "exists";
    }

    public async Task<bool> CreateTextFile(string remoteFilePath, string content)
    {
        Connect();
        var createFileCmd = $"cat > \"{remoteFilePath}\" << 'EOL'\n{content}\nEOL";
        await SendCommand(createFileCmd);
        return await FileExists(remoteFilePath);
    }

    public async Task<bool> DeleteDirectory(string remotePath)
    {
        Connect();
        var dirExistsCheck =
            await SendCommand($"test -d \"{remotePath}\" && echo \"exists\" || echo \"not found\"");

        if (dirExistsCheck.Trim() == "exists")
        {
            await SendCommand($"rm -rf \"{remotePath}\"");
        }

        var afterCheck = await SendCommand($"test -d \"{remotePath}\" && echo \"exists\" || echo \"not found\"");
        return afterCheck.Trim() == "not found";
    }

    public Task MakeExecutable(string remotePath)
    {
        return SendCommand($"chmod +x '{remotePath}'");
    }

    public async Task<string> GetFileContent(string filePath)
    {
        return await SendCommand($"cat '{filePath}' 2>/dev/null || echo 'No output file found'");
    }

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

    ~LinuxSshConnection()
    {
        Dispose(false);
    }
}
