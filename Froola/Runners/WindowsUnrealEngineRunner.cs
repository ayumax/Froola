using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
    private readonly IProcessRunner _processRunner = processRunner;
    private readonly string _unrealBasePath = windowOptions.Value.WindowsUnrealBasePath;

    /// <inheritdoc cref="IUnrealEngineRunner" />
    public async Task BuildPlugin(string pluginPath, string outputPath, int engineVersion, string targetPlatforms,
        string logFilePath = "", Dictionary<string, string>? environmentVariables = null)
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

            await foreach (var item in _processRunner.RunAsync(runFile, args, workDirectory, environmentVariables))
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

    /// <inheritdoc cref="IUnrealEngineRunner" />
    public async Task RunUnrealEditor(string editorPath, string arguments, string workingDirectory,
        string logFilePath = "", Dictionary<string, string>? environmentVariables = null)
    {
        logger.LogInformation($"Running UnrealEditor command: {editorPath} {arguments}");

        StreamWriter? writer = null;

        try
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                writer = new StreamWriter(logFilePath);
            }

            await foreach (var line in _processRunner.RunAsync(editorPath, arguments, workingDirectory,
                               environmentVariables))
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

    /// <inheritdoc cref="IUnrealEngineRunner" />
    public async Task RunBuildScript(string buildScriptPath, string arguments, string workingDirectory,
        string logFilePath = "", Dictionary<string, string>? environmentVariables = null)
    {
        logger.LogInformation($"Running build script: {buildScriptPath} {arguments}");

        StreamWriter? writer = null;

        try
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                writer = new StreamWriter(logFilePath);
            }

            await foreach (var line in _processRunner.RunAsync(buildScriptPath, arguments, workingDirectory,
                               environmentVariables))
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