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
    public Task<bool> IsDockerReady()
    {
        // ToDo : Threre is no way to check if docker is ready right now
        return Task.FromResult(true);
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

    /// <summary>
    /// Prepares custom plugins for Docker by copying them to a staging directory.
    /// </summary>
    /// <param name="sourcePath">The path to the directory containing plugins on the host machine.</param>
    /// <param name="projectDir">The project directory on the host that's mounted to Docker.</param>
    /// <returns>Path to the staged plugins directory, or null if operation failed.</returns>
    public Task<string?> PreparePluginsForDockerAsync(string sourcePath, string projectDir)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            logger.LogInformation("Plugin source path is empty, skipping plugin preparation");
            return Task.FromResult<string?>(null);
        }

        if (!fileSystem.DirectoryExists(sourcePath))
        {
            logger.LogError($"Plugin source directory not found at {sourcePath}");
            return Task.FromResult<string?>(null);
        }

        try
        {
            // Create a staging directory for plugins in the project directory
            var pluginsStageDir = Path.Combine(projectDir, "PluginsStage");
            if (fileSystem.DirectoryExists(pluginsStageDir))
            {
                fileSystem.DeleteDirectory(pluginsStageDir, true);
            }
            fileSystem.CreateDirectory(pluginsStageDir);

            // Copy all plugins from source to staging directory
            logger.LogInformation($"Copying plugins from {sourcePath} to staging directory");
            fileSystem.CopyDirectory(sourcePath, pluginsStageDir);

            logger.LogInformation($"Successfully prepared plugins for Docker container at {pluginsStageDir}");
            return Task.FromResult<string?>(pluginsStageDir);
        }
        catch (Exception ex)
        {
            logger.LogError($"Error preparing plugins for Docker: {ex.Message}", ex);
            return Task.FromResult<string?>(null);
        }
    }
}