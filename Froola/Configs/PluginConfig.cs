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
///     Class containing all input parameters required for plugin build, test, and packaging operations.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[OptionClass("Plugin")]
public class PluginConfig : IFroolaMergeConfig<PluginConfig>
{
    /// <summary>
    ///     Name of the plugin
    /// </summary>
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    ///     Name of the project
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    ///     List of target editor platforms (OS) to build/test
    /// </summary>
    public OptionList<EditorPlatform> EditorPlatforms { get; set; } = [];

    /// <summary>
    ///     List of Unreal Engine versions to use
    /// </summary>
    public OptionList<UEVersion> EngineVersions { get; set; } = [];

    /// <summary>
    ///     Path to store build/test results
    /// </summary>
    public string ResultPath { get; set; } = string.Empty;

    /// <summary>
    ///     Whether to run test
    /// </summary>
    public bool RunTest { get; set; } = false;

    /// <summary>
    ///     Whether to run package
    /// </summary>
    public bool RunPackage { get; set; } = false;

    /// <summary>
    ///     List of game platforms for packaging
    /// </summary>
    public OptionList<GamePlatform> PackagePlatforms { get; set; } = [];

    /// <summary>
    ///     Indicates whether the plugin output should be compressed into a zip file.
    /// </summary>
    public bool IsZipped { get; set; } = true;

    /// <summary>
    ///     Whether to keep the Binary directory after the operation.
    /// </summary>
    public bool KeepBinaryDirectory { get; set; } = false;

    /// <summary>
    ///     Whether to copy packaged plugin to configured destination paths after successful packaging
    /// </summary>
    public bool CopyPackageAfterBuild { get; set; } = false;


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

    public PluginConfig Build()
    {
        var resultPath = string.IsNullOrEmpty(ResultPath)
            ? Path.Combine(AppContext.BaseDirectory, "outputs")
            : ResultPath;

        ResultPath = Path.Combine(resultPath, $"{DateTime.Now:yyyyMMdd_HHmmss}_{PluginName}");

        if (!Path.IsPathRooted(ResultPath))
        {
            ResultPath = Path.GetFullPath(ResultPath);
        }

        if (string.IsNullOrWhiteSpace(PluginName))
        {
            throw new ArgumentException("PluginName must not be empty");
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