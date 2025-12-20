using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Froola.Configs.Attributes;
using Froola.Configs.Collections;
using Froola.Interfaces;

namespace Froola.Configs;

/// <summary>
///     Class containing all input parameters required for project packaging operations.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[OptionClass("Package")]
public class PackageConfig : IFroolaMergeConfig<PackageConfig>
{
    /// <summary>
    ///     Name of the project
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    ///     List of target editor platforms (OS) to build
    /// </summary>
    public OptionList<EditorPlatform> EditorPlatforms { get; set; } = [];

    /// <summary>
    ///     List of Unreal Engine versions to use
    /// </summary>
    public OptionList<UEVersion> EngineVersions { get; set; } = [];

    /// <summary>
    ///     Path to store packaging results
    /// </summary>
    public string ResultPath { get; set; } = string.Empty;

    /// <summary>
    ///     List of game platforms for packaging
    /// </summary>
    public OptionList<GamePlatform> PackagePlatforms { get; set; } = [];

    /// <summary>
    ///     Indicates whether the package output should be compressed into a zip file.
    /// </summary>
    public bool IsZipped { get; set; } = true;

    /// <summary>
    ///     Name of the zip package
    /// </summary>
    public string ZipPackageName { get; set; } = string.Empty;

    /// <summary>
    ///     Environment variables
    /// </summary>
    /// <remarks>Format of VALUENAME=value</remarks>
    public OptionList<string> EnvironmentVariables { get; set; } = [];

    /// <summary>
    ///     Environment variable map
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, string> EnvironmentVariableMap { get; private set; } = new();

    public PackageConfig Build()
    {
        var resultPath = string.IsNullOrEmpty(ResultPath)
            ? Path.Combine(AppContext.BaseDirectory, "outputs")
            : ResultPath;

        ResultPath = Path.Combine(resultPath, $"{DateTime.Now:yyyyMMdd_HHmmss}_{ProjectName}_Package");

        if (!Path.IsPathRooted(ResultPath))
        {
            ResultPath = Path.GetFullPath(ResultPath);
        }

        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            throw new ArgumentException("ProjectName must not be empty");
        }

        if (EditorPlatforms.Count == 0)
        {
            throw new ArgumentException("EditorPlatforms must have at least one value");
        }

        if (EngineVersions.Count == 0)
        {
            throw new ArgumentException("EngineVersions must have at least one value");
        }

        if (PackagePlatforms.Count == 0)
        {
            throw new ArgumentException("PackagePlatforms must have at least one value");
        }

        EnvironmentVariableMap = new Dictionary<string, string>();
        foreach (var keyValuePair in EnvironmentVariables.Select(variablePair => variablePair.Split('=')))
        {
            if (keyValuePair.Length != 2)
            {
                throw new ArgumentException("EnvironmentVariables must be in the format of VALUENAME=value");
            }

            EnvironmentVariableMap.Add(keyValuePair[0], keyValuePair[1]);
        }

        return this;
    }
}
