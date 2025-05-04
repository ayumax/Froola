using System;
using System.Text.Json.Serialization;
using Froola.Configs.Attributes;
using Froola.Interfaces;

namespace Froola.Configs;

/// <summary>
///     Class containing all input parameters required for git
/// </summary>
[OptionClass("Git")]
public class GitConfig : IFroolaMergeConfig<GitConfig>
{
    /// <summary>
    ///     Git repository URL
    /// </summary>
    public string GitRepositoryUrl { get; set; } = string.Empty;

    /// <summary>
    ///     Git branch to use
    /// </summary>
    public string GitBranch { get; set; } = "main";

    /// <summary>
    ///     Path to the SSH key for Git operations
    /// </summary>
    public string GitSshKeyPath { get; set; } = string.Empty;

    public GitConfig Build()
    {
        // GitBranch
        if (string.IsNullOrEmpty(GitBranch))
        {
            throw new ArgumentException("GitBranch must not be null or empty");
        }

        // GitRepositoryUrl
        if (string.IsNullOrEmpty(GitRepositoryUrl))
        {
            throw new ArgumentException("GitRepositoryUrl must not be null or empty");
        }

        return this;
    }
}