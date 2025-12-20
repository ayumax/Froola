using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Diagnostics;
using Froola.Commands.Plugin;
using Froola.Configs;
using Froola.Interfaces;
using Froola.Utils;

namespace Froola.Commands.Package.Builder;

/// <summary>
///     WindowsBuilder handles project packaging processes for the Windows platform.
/// </summary>
[SuppressMessage("ReSharper", "InvertIf")]
public class WindowsBuilder(
    PackageConfig packageConfig,
    WindowsConfig windowsConfig,
    MacConfig macConfig,
    IUnrealEngineRunner unrealRunner,
    IFileSystem fileSystem,
    IFroolaLogger<WindowsBuilder> logger)
    : BuilderBase(packageConfig, windowsConfig, macConfig, logger, fileSystem), IWindowsBuilder
{
    private readonly PackageConfig _packageConfig = packageConfig;
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <summary>
    ///     Gets the editor platform type for Windows.
    /// </summary>
    protected override EditorPlatform MyEditorPlatform => EditorPlatform.Windows;

    /// <summary>
    /// Not implemented for project-only builder.
    /// </summary>
    public Task<BuildResult> Run(UEVersion engineVersion)
    {
        throw new NotImplementedException("Run is not used for project packaging. Use RunPackage instead.");
    }

    /// <summary>
    /// Runs Windows project packaging for the specified engine version.
    /// </summary>
    public async Task<BuildResult> RunPackage(UEVersion engineVersion)
    {
        var result = new BuildResult
        {
            Os = EditorPlatform.Windows,
            EngineVersion = engineVersion,
            StatusOfBuild = BuildStatus.None,
            StatusOfTest = BuildStatus.None,
            StatusOfPackage = BuildStatus.None
        };

        try
        {
            var workingDirectory = Path.GetDirectoryName(ProjectFilePath)!;
            var packageOutDir = PackageDir;

            var buildCookRunArgs = UECommandsHelper.GetBuildCookRunArgs(ProjectFilePath, packageOutDir,
                MyPlatform(), EditorPlatform.Windows);

            var logFilePath = Path.Combine(packageOutDir, "BuildCookRun.log");

            logger.LogInformation("Starting project packaging process...");
            await unrealRunner.RunBuildScript(RunUatBatPath, buildCookRunArgs, workingDirectory, logFilePath,
                _packageConfig.EnvironmentVariableMap);
            logger.LogInformation("Project packaging completed successfully.");

            result.StatusOfPackage = CheckProjectPackageBuildResult(engineVersion, _packageConfig.ProjectName);

            if (result.StatusOfPackage == BuildStatus.Success)
            {
                ZipPackage(engineVersion);
            }
        }
        catch (ProcessErrorException ex)
        {
            if (ex.ExitCode != 0)
            {
                logger.LogError($"Project packaging failed: {ex.Message}");
                result.StatusOfPackage = BuildStatus.Failed;
            }
            else
            {
                result.StatusOfPackage = CheckProjectPackageBuildResult(engineVersion, _packageConfig.ProjectName);
            }
        }

        return result;
    }

    /// <inheritdoc cref="IBuilder" />
    public override Task PrepareRepository(string baseRepositoryPath, UEVersion engineVersion)
    {
        RepositoryPath = Path.Combine(baseRepositoryPath, "..", "Windows");

        _fileSystem.CopyDirectory(baseRepositoryPath, RepositoryPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up the temporary directory for Windows builds.
    /// </summary>
    public Task CleanupTempDirectory()
    {
        try
        {
            _fileSystem.DeleteDirectory(RepositoryPath, true);
        }
        catch (Exception e)
        {
            logger.LogWarning(e.Message);
        }
       
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Gets the game platforms for the specified input parameters.
    /// </summary>
    protected override List<GamePlatform> MyPlatform()
    {
        return _packageConfig.PackagePlatforms.Where(x => x is GamePlatform.Win64 or GamePlatform.Android).ToList();
    }
}
