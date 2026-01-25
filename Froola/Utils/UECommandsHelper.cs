using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Froola.Configs;

namespace Froola.Utils;

/// <summary>
/// Static helper class for Unreal Engine command line arguments and related utilities.
/// </summary>
public static class UECommandsHelper
{
    /// <summary>
    /// Gets the .uproject file path for the specified project and platform.
    /// </summary>
    public static string GetUprojectPath(string repositoryPath, string projectName, EditorPlatform platform)
    {
        return CombinePath(platform, repositoryPath, $"{projectName}.uproject");
    }

    /// <summary>
    /// Gets build command arguments for Unreal Engine.
    /// </summary>
    /// <param name="projectName">Project name.</param>
    /// <param name="projectFilePath">Full path to the project file.</param>
    /// <param name="platform">Target platform (Win64, Mac, Linux).</param>
    /// <param name="configuration">Build configuration (Development, Shipping, etc.).</param>
    /// <returns>Command line arguments for build command.</returns>
    public static string GetBuildCommandArgs(string projectName, string projectFilePath,
        EditorPlatform platform,
        string configuration = "Development")
    {
        var buildPlatform = platform switch
        {
            EditorPlatform.Windows => "Win64",
            EditorPlatform.Mac => "Mac",
            EditorPlatform.Linux => "Linux",
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };

        return $"{projectName}Editor {buildPlatform} {configuration} -Project={projectFilePath} -TargetType=Editor";
    }

    /// <summary>
    /// Gets automation test command arguments for Unreal Engine.
    /// </summary>
    /// <param name="projectFilePath">Full path to the project file.</param>
    /// <param name="pluginName">Plugin name to test.</param>
    /// <param name="reportExportPath">Path to export test reports.</param>
    /// <param name="os">Platform on which the tests run.</param>
    /// <returns>Command line arguments for test command.</returns>
    public static string GetAutomationTestCommandArgs(string projectFilePath, string pluginName,
        string reportExportPath, EditorPlatform os)
    {
        // Common automation flags across all platforms
        const string automationFlags = "-unattended -NullRHI -nosound -nopause -stdout -DDC-ForceMemoryCache -nosplash";
        var automationCmds = $"Automation RunTests {pluginName}";
        const string automationExit = "Automation Test Queue Empty";

        return
            $"{projectFilePath} {automationFlags} -ExecCmds=\"{automationCmds}; quit\" -TestExit=\"{automationExit}\" -ReportExportPath={reportExportPath}";
    }

    /// <summary>
    /// Gets Unreal Engine directory path based on OS and engine version.
    /// </summary>
    /// <param name="windowsConfig">Configuration for Windows.</param>
    /// <param name="macConfig">Configuration for macOS.</param>
    /// <param name="engineVersion">Engine version string (e.g. "3", "4", "5").</param>
    /// <param name="os">Target operating system.</param>
    /// <returns>Path to the Unreal Engine directory.</returns>
    public static string GetUeDirectoryPath(WindowsConfig windowsConfig, MacConfig macConfig, UEVersion engineVersion,
        EditorPlatform os)
    {
        return os switch
        {
            EditorPlatform.Windows =>
                CombinePath(os, windowsConfig.WindowsUnrealBasePath, $"UE_{engineVersion.ToVersionString()}"),
            EditorPlatform.Mac =>
                CombinePath(os, macConfig.MacUnrealBasePath, $"UE_{engineVersion.ToVersionString()}"),
            EditorPlatform.Linux => "/home/ue4/UnrealEngine",
            _ => throw new ArgumentOutOfRangeException(nameof(os), os, null)
        };
    }

    /// <summary>
    /// Gets Unreal Editor executable path based on OS and engine version.
    /// </summary>
    /// <param name="unrealEngineDir">Unreal Engine directory path.</param>
    /// <param name="os">Target operating system.</param>
    /// <returns>Path to the Unreal Editor executable.</returns>
    public static string GetUnrealEditorPath(string unrealEngineDir, EditorPlatform os)
    {
        return os switch
        {
            EditorPlatform.Windows =>
                CombinePath(os, unrealEngineDir, "Engine", "Binaries", "Win64", "UnrealEditor-Cmd.exe"),
            EditorPlatform.Mac =>
                CombinePath(os, unrealEngineDir, "Engine", "Binaries", "Mac", "UnrealEditor-Cmd"),
            EditorPlatform.Linux =>
                CombinePath(os, unrealEngineDir, "Engine", "Binaries", "Linux", "UnrealEditor-Cmd"),
            _ => throw new ArgumentOutOfRangeException(nameof(os), os, null)
        };
    }

    /// <summary>
    /// Gets build script path based on OS and engine version.
    /// </summary>
    /// <param name="unrealEngineDir">Unreal Engine directory path.</param>
    /// <param name="os">Target operating system.</param>
    /// <returns>Path to the build script.</returns>
    public static string GetBuildScriptPath(string unrealEngineDir, EditorPlatform os)
    {
        return os switch
        {
            EditorPlatform.Windows => CombinePath(os, unrealEngineDir, "Engine", "Build", "BatchFiles", "Build.bat"),
            EditorPlatform.Mac => CombinePath(os, unrealEngineDir, "Engine", "Build", "BatchFiles", "Mac", "Build.sh"),
            EditorPlatform.Linux => CombinePath(os, unrealEngineDir, "Engine", "Build", "BatchFiles", "Linux",
                "Build.sh"),
            _ => throw new ArgumentOutOfRangeException(nameof(os), os, null)
        };
    }

    /// <summary>
    /// Gets the full path to RunUAT.bat or platform equivalent for the given Unreal Engine directory and OS.
    /// </summary>
    /// <param name="ueDir">Root directory of Unreal Engine.</param>
    /// <param name="os">Target operating system.</param>
    /// <returns>Full path to RunUAT.bat or platform equivalent.</returns>
    public static string GetRunUatScriptPath(string ueDir, EditorPlatform os)
    {
        var script = os == EditorPlatform.Windows ? "RunUAT.bat" : "RunUAT.sh";

        return CombinePath(os, ueDir, "Engine", "Build", "BatchFiles", script);
    }

    /// <summary>
    /// Gets the .uplugin file path for the specified plugin and platform.
    /// </summary>
    public static string GetUpluginPath(string repositoryPath, string projectName, EditorPlatform platform)
    {
        return CombinePath(platform, repositoryPath, "Plugins", projectName, $"{projectName}.uplugin");
    }

    /// <summary>
    /// Gets BuildPlugin arguments for RunUAT.
    /// </summary>
    /// <param name="repoPath">Path to the repository.</param>
    /// <param name="pluginName">Name of the plugin.</param>
    /// <param name="outputDir">Output directory for the packaged plugin.</param>
    /// <param name="targetPlatforms">Comma-separated list of target platforms.</param>
    /// <param name="editorPlatform">Target editor platform.</param>
    /// <returns>Arguments string for BuildPlugin.</returns>
    public static string GetBuildPluginArgs(string repoPath, string pluginName, string outputDir,
        IEnumerable<GamePlatform> targetPlatforms, EditorPlatform editorPlatform)
    {
        var upluginPath = GetUpluginPath(repoPath, pluginName, editorPlatform);

        var platforms =
            targetPlatforms.Aggregate(string.Empty, (current, platform) => current + (platform + "+"));

        platforms = platforms.TrimEnd('+');

        var packageOutDir = CombinePath(editorPlatform, outputDir, "Plugin");
        
        return
            $"BuildPlugin -Plugin={upluginPath} -Package={packageOutDir} -TargetPlatforms={platforms}";
    }

    /// <summary>
    /// Gets BuildCookRun arguments for RunUAT.
    /// </summary>
    /// <param name="projectFilePath">Path to the .uproject file.</param>
    /// <param name="outputDir">Output directory for the packaged game.</param>
    /// <param name="targetPlatform">Target game platform.</param>
    /// <param name="editorPlatform">Target editor platform.</param>
    /// <returns>Arguments string for BuildCookRun.</returns>
    public static string GetBuildCookRunArgs(string projectFilePath, string outputDir, GamePlatform targetPlatform, EditorPlatform editorPlatform)
    {
        var platformStr = targetPlatform.ToString();
        var extraArgs = string.Empty;

        if (editorPlatform == EditorPlatform.Mac && targetPlatform == GamePlatform.Mac)
        {
            // For UE5.4+ on Apple Silicon Mac, UAT may require an explicit architecture when building Mac targets.
            // Adding -architecture=arm64 has been observed to fix the "Platform Mac is not a valid platform to build" error
            // in our Apple Silicon environment. If other editor/target platform combinations later require explicit
            // architectures, extend this logic here (or make it data-driven) rather than adding ad-hoc flags elsewhere.
            extraArgs = " -architecture=arm64";
        }
        
        return $"BuildCookRun -project={projectFilePath} -archive -archivedirectory={outputDir} -platform={platformStr}{extraArgs} -clientconfig=Shipping -nop4 -build -cook -stage -pak -allmaps -nocompileeditor -unattended -utf8";
    }

    /// <summary>
    /// Gets the path to the GenerateProjectFiles script.
    /// </summary>
    /// <param name="windowsConfig">Configuration for Windows.</param>
    /// <param name="macConfig">Configuration for macOS.</param>
    /// <param name="engineVersion">Engine version</param>
    /// <param name="editorPlatform">Target editor platform.</param>
    /// <returns>Path to the GenerateProjectFiles script.</returns>
    public static string GetGenerateProjectFiles(WindowsConfig windowsConfig, MacConfig macConfig,
        UEVersion engineVersion,
        EditorPlatform editorPlatform)
    {
        var unrealEngineDir = GetUeDirectoryPath(windowsConfig, macConfig, engineVersion, editorPlatform);

        return editorPlatform switch
        {
            EditorPlatform.Windows =>
                // Not available for Windows
                string.Empty,
            EditorPlatform.Mac =>
                CombinePath(editorPlatform, unrealEngineDir, "Engine", "Build", "BatchFiles", "Mac",
                    "GenerateProjectFiles.sh"),
            EditorPlatform.Linux =>
                CombinePath(editorPlatform, unrealEngineDir, "Engine", "Build", "BatchFiles", "Linux",
                    "GenerateProjectFiles.sh"),
            _ => throw new ArgumentOutOfRangeException(nameof(editorPlatform), editorPlatform, null)
        };
    }

    /// <summary>
    /// Combines multiple paths into a single path based on the target OS.
    /// </summary>
    private static string CombinePath(EditorPlatform os, params ReadOnlySpan<string> paths)
    {
        return os switch
        {
            EditorPlatform.Windows => Path.Combine(paths),
            EditorPlatform.Mac or EditorPlatform.Linux =>
                Path.Combine(paths).Replace('\\', '/') // Use forward slashes for Mac paths()
            ,
            _ => throw new ArgumentException($"Unsupported OS: {os}")
        };
    }
}