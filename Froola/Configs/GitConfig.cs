using System;
using System.IO;
using System.Text.Json.Serialization;
using Froola.Configs.Attributes;
using Froola.Configs.Collections;
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
    ///     Git branch to use (if the version is not specified in GitBranches, this value is used)
    /// </summary>
    public string GitBranch { get; set; } = "main";

    /// <summary>
    ///     Dictionary of Git branches per Unreal Engine version (e.g. "main", "UE5.3")
    /// </summary>
    public OptionDictionary GitBranches { get; set; } = new();

    [JsonIgnore] public OptionDictionary<UEVersion, string> GitBranchesWithVersion { get; set; } = new(); 

    /// <summary>
    ///     Path to the SSH key for Git operations
    /// </summary>
    public string GitSshKeyPath { get; set; } = string.Empty;

    /// <summary>
    ///     Path to the local repository
    /// </summary>
    /// <remarks>
    ///     If this is specified, the local repo will use instead of git remote repository
    /// </remarks>
    public string LocalRepositoryPath { get; set; } = string.Empty;

    public GitConfig Build()
    {
        if (string.IsNullOrWhiteSpace(LocalRepositoryPath))
        {
            if (GitBranches.Count == 0)
            {
                // GitBranch
                if (string.IsNullOrEmpty(GitBranch))
                {
                    throw new ArgumentException("GitBranch must not be null or empty");
                }
            }

            // GitRepositoryUrl
            if (string.IsNullOrEmpty(GitRepositoryUrl))
            {
                throw new ArgumentException("GitRepositoryUrl must not be null or empty");
            }
        }
        else
        {
            // LocalRepositoryPath
            if (!Directory.Exists(LocalRepositoryPath))
            {
                throw new ArgumentException("LocalRepositoryPath does not exist");
            }
        }

        // GitBranches, GitBranchesWithVersion
        GitBranchesWithVersion.Clear();
        foreach (var (key, value) in GitBranches)
        {
            GitBranchesWithVersion.Add(UEVersionExtensions.Parse(key), value);
        }
        
        return this;
    }
}