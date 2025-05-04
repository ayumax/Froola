using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cysharp.Diagnostics;
using Froola.Interfaces;

namespace Froola.Utils;

/// <summary>
/// This class provides utility methods for Git operations such as cloning repositories and converting URLs.
/// </summary>
public partial class GitClient(IFroolaLogger<GitClient> logger, IProcessRunner processRunner)
    : IGitClient
{
    /// <summary>
    /// Path to SSH private key for Git operations
    /// </summary>
    public string SshKeyPath { get; set; } = string.Empty;

    /// <summary>
    /// Converts a GitHub HTTPS repository URL to SSH format. If the URL is already in SSH format, returns it as is.
    /// </summary>
    public string ConvertHttpsToSshUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return url;
        }

        // Return as is if already in SSH format
        if (url.StartsWith("git@"))
        {
            return url;
        }

        // GitHub HTTPS pattern: https://github.com/username/repo.git
        var regex = MyRegex();
        var match = regex.Match(url);

        if (!match.Success)
        {
            return url;
        }

        var username = match.Groups[1].Value;
        var repo = match.Groups[2].Value;

        // Add .git if not included in the original URL
        if (!repo.EndsWith(".git"))
        {
            repo += ".git";
        }

        return $"git@github.com:{username}/{repo}";
    }

    /// <summary>
    /// Clones a Git repository to the specified target path. Supports both HTTPS and SSH URLs.
    /// </summary>
    public async Task<bool> CloneRepository(string repoUrl, string branch, string targetPath)
    {
        try
        {
            logger.LogInformation($"Cloning repository {repoUrl} to {targetPath}");

            // Create directory if it doesn't exist
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            // Convert HTTPS URL to SSH format if SSH key is specified
            if (string.IsNullOrEmpty(SshKeyPath) || !File.Exists(SshKeyPath))
            {
                return await ExecuteGitCommand($"clone {repoUrl} -b {branch} .", targetPath);
            }

            var originalUrl = repoUrl;
            repoUrl = ConvertHttpsToSshUrl(repoUrl);

            if (originalUrl != repoUrl)
            {
                logger.LogInformation(
                    $"Converting HTTPS URL to SSH format: {originalUrl} -> {repoUrl}");
            }

            // Execute git clone command
            return await ExecuteGitCommand($"clone {repoUrl} -b {branch} .", targetPath);
        }
        catch (Exception ex)
        {
            logger.LogError($"Error cloning repository: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Executes a Git command and returns the output as a string.
    /// </summary>
    private async Task<string> RunGitCommand(string arguments, string workingDirectory)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        var gitCommand = "git";
        var sshCommand = string.Empty;

        // Set GIT_SSH_COMMAND if SSH key is specified
        if (!string.IsNullOrEmpty(SshKeyPath) && File.Exists(SshKeyPath))
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                sshCommand = $"ssh -i \"{SshKeyPath}\" -o IdentitiesOnly=yes";
            }
            else
            {
                sshCommand = $"ssh -i {SshKeyPath} -o IdentitiesOnly=yes";
            }
        }

        var env = string.IsNullOrEmpty(sshCommand)
            ? null
            : new Dictionary<string, string> { { "GIT_SSH_COMMAND", sshCommand } };

        await foreach (var result in env == null
            ? processRunner.RunAsync(gitCommand, arguments, workingDirectory)
            : processRunner.RunAsync(gitCommand, arguments, workingDirectory, env))
        {
            outputBuilder.AppendLine(result);
        }

        // NOTE: ProcessX throws if exit code != 0, so error handling/logging can be done via catch
        return outputBuilder.ToString();
    }

    /// <summary>
    /// Executes a Git command and returns true if successful, false otherwise.
    /// </summary>
    private async Task<bool> ExecuteGitCommand(string arguments, string workingDirectory)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        var gitCommand = "git";
        var sshCommand = string.Empty;

        // Set GIT_SSH_COMMAND if SSH key is specified
        if (!string.IsNullOrEmpty(SshKeyPath) && File.Exists(SshKeyPath))
        {
            logger.LogInformation($"Using SSH key: {SshKeyPath}");
            sshCommand = Environment.OSVersion.Platform == PlatformID.Win32NT
                ? $"ssh -i \"{SshKeyPath}\" -o IdentitiesOnly=yes"
                : $"ssh -i {SshKeyPath} -o IdentitiesOnly=yes";
        }
        else
        {
            logger.LogInformation($"Warning: SSH key not found at {SshKeyPath}");
        }

        var env = string.IsNullOrEmpty(sshCommand)
            ? null
            : new Dictionary<string, string> { { "GIT_SSH_COMMAND", sshCommand } };

        try
        {
            await foreach (var line in env == null
                ? processRunner.RunAsync(gitCommand, arguments, workingDirectory)
                : processRunner.RunAsync(gitCommand, arguments, workingDirectory, env))
            {
                outputBuilder.AppendLine(line);
                logger.LogInformation($"Git: {line}");
            }

            return true;
        }
        catch (ProcessErrorException ex)
        {
            // Ignore error if exit code is 0 (success)
            if (ex.ExitCode == 0)
            {
                logger.LogInformation($"Git (stderr, exit 0): {ex.Message}");
                return true;
            }
            logger.LogInformation($"Git Error: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Git Error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Returns a Regex object for matching GitHub HTTPS URLs.
    /// </summary>
    [GeneratedRegex(@"https://github\.com/([^/]+)/(.+?)(?:\.git)?$")]
    private static partial Regex MyRegex();
}