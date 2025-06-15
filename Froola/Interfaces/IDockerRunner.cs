using System.Collections.Generic;
using System.Threading.Tasks;

namespace Froola.Interfaces;

/// <summary>
/// Interface for Docker operations such as running containers and copying scripts.
/// </summary>
public interface IDockerRunner
{
    /// <summary>
    ///     Whether Docker is available
    /// </summary>
    /// <returns>true:available</returns>
    Task<bool> IsDockerReady();
    
    /// <summary>
    /// Runs a command in a Docker container and streams the output.
    /// </summary>
    /// <param name="imageName">Docker image name.</param>
    /// <param name="command">Command to run inside the container.</param>
    /// <param name="workingDirectory">Working directory on the host.</param>
    /// <param name="volumeMappings">Volume mappings (host:container).</param>
    /// <param name="environmentVariables">Environment variables for the container.</param>
    /// <returns>Async enumerable of command output lines.</returns>
    IAsyncEnumerable<string> RunContainer(
        string imageName,
        string command,
        string workingDirectory,
        Dictionary<string, string> volumeMappings,
        Dictionary<string, string> environmentVariables);

    /// <summary>
    /// Copies a script to the Docker volume and ensures LF line endings.
    /// </summary>
    /// <param name="localScriptPath">Local path of the script.</param>
    /// <param name="projectDir">Host project directory.</param>
    /// <param name="projectDirInDocker">Project directory path inside the Docker container.</param>
    /// <returns>Path to the script inside Docker container, or null if not found.</returns>
    Task<string?> CopyScriptToDockerAndNormalizeAsync(string localScriptPath, string projectDir,
        string projectDirInDocker);

    /// <summary>
    /// Prepares custom plugins for Docker by copying them to a staging directory.
    /// </summary>
    /// <param name="sourcePath">The path to the directory containing plugins on the host machine.</param>
    /// <param name="projectDir">The project directory on the host that's mounted to Docker.</param>
    /// <returns>Path to the staged plugins directory, or null if operation failed.</returns>
    Task<string?> PreparePluginsForDockerAsync(string sourcePath, string projectDir);
}