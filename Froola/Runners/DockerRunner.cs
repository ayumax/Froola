using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Froola.Configs;
using Froola.Interfaces;

namespace Froola.Runners;

/// <summary>
/// Implementation of Docker runner for executing commands in Docker containers and handling file operations.
/// </summary>
public class DockerRunner(
    IFroolaLogger<DockerRunner> logger,
    IFileSystem fileSystem,
    IProcessRunner processRunner,
    LinuxConfig linuxConfig) : IDockerRunner
{
    /// <inheritdoc />
      public async Task<bool> IsDockerReady()
    {
        // Try to execute "<dockerCommand> info" and check if it succeeds.
        // If any error occurs (including command not found), return false.
        var command = linuxConfig.DockerCommand;
        const string args = "info";
        var hasError = false;
        try
        {
            await foreach (var line in processRunner.RunAsync(command, args, Directory.GetCurrentDirectory(),
                               new Dictionary<string, string>()))
            {
                // If any line contains 'ERROR', treat as not ready
                if (!string.IsNullOrWhiteSpace(line) && line.Contains("ERROR", StringComparison.CurrentCultureIgnoreCase))
                {
                    hasError = true;
                }
            }
        }
        catch (Exception)
        {
            // If any exception occurs (e.g., command not found), Docker is not ready
            return false;
        }
        return !hasError;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> RunContainer(
        string imageName,
        string command,
        string workingDirectory,
        Dictionary<string, string> volumeMappings,
        Dictionary<string, string> environmentVariables)
    {
        // Build the docker run command
        var dockerArgs = volumeMappings.Aggregate("run --rm",
            (current, mapping) => current + $" -v \"{mapping.Key}\":\"{mapping.Value}\"");

        // Add environment variables
        dockerArgs =
            environmentVariables.Aggregate(dockerArgs, (current, env) => current + $" -e {env.Key}={env.Value}");

        // Add image and command
        dockerArgs += $" {imageName} /bin/bash -c \"{command}\"";

        logger.LogInformation($"Running Docker command: docker {dockerArgs}");

        await foreach (var line in processRunner.RunAsync(linuxConfig.DockerCommand, dockerArgs, workingDirectory))
        {
            yield return line;
        }
    }

    /// <inheritdoc />
    public async Task<string?> CopyScriptToDockerAndNormalizeAsync(string localScriptPath,
        string projectDir, string projectDirInDocker)
    {
        // Check if the script exists
        if (!fileSystem.FileExists(localScriptPath))
        {
            logger.LogError($"Script not found at {localScriptPath}");
            return null;
        }

        var dockerScriptPath = Path.Combine(projectDir, Path.GetFileName(localScriptPath));

        logger.LogInformation($"Copying script to Docker volume: {dockerScriptPath}");
        fileSystem.FileCopy(localScriptPath, dockerScriptPath, true);

        // Ensure Unix-style line endings (LF only)
        var scriptContent = await fileSystem.FileReadAllTextAsync(dockerScriptPath);
        scriptContent = scriptContent.Replace("\r\n", "\n");
        await fileSystem.WriteAllTextAsync(dockerScriptPath, scriptContent);

        return Path.Combine(projectDirInDocker, Path.GetFileName(localScriptPath)).Replace('\\', '/');
    }
}