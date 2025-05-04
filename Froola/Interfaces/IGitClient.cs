using System.Threading.Tasks;

namespace Froola.Interfaces;

/// <summary>
/// Interface for Git operations such as cloning, pulling, and converting URLs.
/// </summary>
public interface IGitClient
{
    string SshKeyPath { get; set; }

    /// <summary>
    /// Converts an HTTPS Git URL to SSH format.
    /// </summary>
    /// <param name="url">HTTPS Git URL.</param>
    /// <returns>SSH Git URL.</returns>
    string ConvertHttpsToSshUrl(string url);

    /// <summary>
    /// Clones a Git repository to the specified path.
    /// </summary>
    /// <param name="repoUrl">URL of the Git repository.</param>
    /// <param name="branch">Branch to clone.</param>
    /// <param name="targetPath">Target directory for the clone.</param>
    /// <returns>True if the clone operation succeeded.</returns>
    Task<bool> CloneRepository(string repoUrl, string branch, string targetPath);
}