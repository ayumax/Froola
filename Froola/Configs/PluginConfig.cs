using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
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

    public PluginConfig Build()
    {
        var resultPath = string.IsNullOrEmpty(ResultPath)
            ? Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)!, "outputs")
            : ResultPath;

        ResultPath = Path.Combine(resultPath, $"{DateTime.Now:yyyyMMdd_HHmmss}_{PluginName}");

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

        return this;
    }
}