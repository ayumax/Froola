using System.IO;
using System.Threading.Tasks;
using Cysharp.Diagnostics;
using Froola.Configs;
using Froola.Interfaces;
using Microsoft.Extensions.Options;

namespace Froola.Runners;

/// <summary>
/// Windows implementation of Unreal Engine runner for building plugins and running the Unreal Editor.
/// </summary>
public class WindowsUnrealEngineRunner(
    IFroolaLogger<WindowsUnrealEngineRunner> logger,
    IOptions<WindowsConfig> windowOptions,
    IProcessRunner processRunner) : IUnrealEngineRunner
{
    private readonly IProcessRunner processRunner = processRunner;
    private readonly string _unrealBasePath = windowOptions.Value.WindowsUnrealBasePath;

    /// <summary>
    /// Builds a plugin using the Unreal Engine AutomationTool.
    /// </summary>
    /// <param name="pluginPath">Path to the .uplugin file.</param>
    /// <param name="outputPath">Output directory for the built plugin.</param>
    /// <param name="engineVersion">Engine version to use.</param>
    /// <param name="targetPlatforms">Comma-separated list of target platforms.</param>
    /// <param name="logFilePath">Optional path to a log file.</param>
    public async Task BuildPlugin(string pluginPath, string outputPath, int engineVersion, string targetPlatforms,
        string logFilePath = "")
    {
        var runFile = Path.Combine(_unrealBasePath, $"UE_5.{engineVersion}", "Engine", "Binaries", "DotNET",
            "AutomationTool", "AutomationTool.exe");
        var args =
            $"BuildPlugin -Plugin={pluginPath} -Package={outputPath} -CreateSubFolder -TargetPlatforms={targetPlatforms} -NoSplash -Unattended -NullRHI\"";
        var workDirectory = Path.GetDirectoryName(pluginPath)!;

        logger.LogInformation($"Running UE build command: {runFile} {args}");

        StreamWriter? writer = null;

        try
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                writer = new StreamWriter(logFilePath);
            }

            await foreach (var item in processRunner.RunAsync(runFile, args, workDirectory))
            {
                logger.LogInformation(item);
            }
        }
        finally
        {
            if (writer is not null)
            {
                await writer.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Runs the Unreal Editor with the specified command line arguments.
    /// </summary>
    /// <param name="editorPath">Path to the Unreal Editor executable.</param>
    /// <param name="arguments">Command line arguments for the editor.</param>
    /// <param name="workingDirectory">Working directory for the editor process.</param>
    /// <param name="logFilePath">Optional path to a log file.</param>
    public async Task RunUnrealEditor(string editorPath, string arguments, string workingDirectory,
        string logFilePath = "")
    {
        logger.LogInformation($"Running UnrealEditor command: {editorPath} {arguments}");

        StreamWriter? writer = null;

        try
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                writer = new StreamWriter(logFilePath);
            }

            await foreach (var line in processRunner.RunAsync(editorPath, arguments, workingDirectory))
            {
                logger.LogInformation(line);

                if (writer is not null)
                {
                    await writer.WriteLineAsync(line);
                }
            }
        }
        finally
        {
            if (writer is not null)
            {
                await writer.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Runs a build script with the specified command line arguments.
    /// </summary>
    /// <param name="buildScriptPath">Path to the build script executable.</param>
    /// <param name="arguments">Command line arguments for the script.</param>
    /// <param name="workingDirectory">Working directory for the script process.</param>
    /// <param name="logFilePath">Optional path to a log file.</param>
    public async Task RunBuildScript(string buildScriptPath, string arguments, string workingDirectory,
        string logFilePath = "")
    {
        logger.LogInformation($"Running build script: {buildScriptPath} {arguments}");

        StreamWriter? writer = null;

        try
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                writer = new StreamWriter(logFilePath);
            }

            await foreach (var line in processRunner.RunAsync(buildScriptPath, arguments, workingDirectory))
            {
                logger.LogInformation(line);

                if (writer is not null)
                {
                    await writer.WriteLineAsync(line);
                }
            }
        }
        finally
        {
            if (writer is not null)
            {
                await writer.DisposeAsync();
            }
        }
    }
}