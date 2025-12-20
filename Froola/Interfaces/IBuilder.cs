using System.Threading.Tasks;
using Froola.Commands.Plugin;

namespace Froola.Interfaces;

/// <summary>
/// Interface for OS-specific test and build implementations.
/// </summary>
public interface IBuilder
{
    /// <summary>
    /// Runs a test and build for the specified engine version.
    /// </summary>
    /// <param name="engineVersion">Engine version</param>
    /// <returns>Test result.</returns>
    Task<BuildResult> Run(UEVersion engineVersion);

    /// <summary>
    /// Runs a project package build for the specified engine version.
    /// </summary>
    /// <param name="engineVersion">Engine version</param>
    /// <returns>Build result.</returns>
    Task<BuildResult> RunPackage(UEVersion engineVersion);

    /// <summary>
    /// Prepares the repository for testing.
    /// </summary>
    /// <param name="baseRepositoryPath">Temporary directory path.</param>
    /// <param name="engineVersion">Version of the engine</param>
    /// <returns>Path to the prepared repository.</returns>
    Task PrepareRepository(string baseRepositoryPath, UEVersion engineVersion);

    void InitDirectory(UEVersion engineVersion);
    

    /// <summary>
    /// Cleans up the temporary directory.
    /// </summary>
    Task CleanupTempDirectory();
}