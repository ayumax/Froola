using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Froola.Configs;
using Froola.Interfaces;
using Froola.Utils;

namespace Froola.Commands.Plugin.Builder;

/// <summary>
/// Abstract base class for platform-specific builders. Handles directory and repository setup.
/// </summary>
public abstract class BuilderBase
{
    protected readonly PluginConfig PluginConfig;
    protected readonly WindowsConfig WindowsConfig;
    protected readonly MacConfig MacConfig;
    protected readonly LinuxConfig LinuxConfig;
    protected readonly IFroolaLogger Logger;
    protected readonly ITestResultsEvaluator TestResultsEvaluator;

    protected IFileSystem FileSystem { get; }

    protected BuilderBase(
        PluginConfig pluginConfig,
        WindowsConfig windowsConfig,
        MacConfig macConfig,
        LinuxConfig linuxConfig,
        IFroolaLogger logger,
        IFileSystem fileSystem,
        ITestResultsEvaluator testResultsEvaluator)
    {
        PluginConfig = pluginConfig;
        WindowsConfig = windowsConfig;
        MacConfig = macConfig;
        LinuxConfig = linuxConfig;
        Logger = logger;
        FileSystem = fileSystem;
        TestResultsEvaluator = testResultsEvaluator;
    }

    public string RepositoryPath { get; protected set; } = null!;

    protected string BuildResultDir = null!;
    protected string TestResultDir = null!;
    protected string PackageDir = null!;
    protected string GameDir = null!;
    protected string ProjectFilePath = null!;
    protected string UeDirectoryPath = null!;
    protected string EditorPath = null!;
    protected string BuildBatPath = null!;
    protected string BuildProjectArgs = null!;
    protected string RunUatBatPath = null!;

    /// <summary>
    /// Prepares the repository for building and sets up input parameters.
    /// </summary>
    public abstract Task PrepareRepository(string baseRepositoryPath, UEVersion engineVersion);
    

    /// <summary>
    /// Initializes build, test, and package directories for the specified engine version.
    /// </summary>
    public void InitDirectory(UEVersion engineVersion)
    {
        BuildResultDir = Path.Combine(PluginConfig.ResultPath, "build",
            $"{MyEditorPlatform}_{engineVersion.ToFullVersionString()}");
        if (!FileSystem.DirectoryExists(BuildResultDir))
        {
            FileSystem.CreateDirectory(BuildResultDir);
        }

        TestResultDir = Path.Combine(PluginConfig.ResultPath, "tests",
            $"{MyEditorPlatform}_{engineVersion.ToFullVersionString()}");
        if (!FileSystem.DirectoryExists(TestResultDir))
        {
            FileSystem.CreateDirectory(TestResultDir);
        }

        PackageDir = Path.Combine(PluginConfig.ResultPath, "packages",
            $"{MyEditorPlatform}_{engineVersion.ToFullVersionString()}");
        if (!FileSystem.DirectoryExists(PackageDir))
        {
            FileSystem.CreateDirectory(PackageDir);
        }
        
        GameDir = Path.Combine(PluginConfig.ResultPath, "game",
            $"{MyEditorPlatform}_{engineVersion.ToFullVersionString()}");
        if (!FileSystem.DirectoryExists(GameDir))
        {
            FileSystem.CreateDirectory(GameDir);
        }

        ProjectFilePath =
            UECommandsHelper.GetUprojectPath(RepositoryPath, PluginConfig.ProjectName, MyEditorPlatform);

        UeDirectoryPath =
            UECommandsHelper.GetUeDirectoryPath(WindowsConfig, MacConfig, LinuxConfig, engineVersion, MyEditorPlatform);

        EditorPath = UECommandsHelper.GetUnrealEditorPath(UeDirectoryPath, MyEditorPlatform);

        BuildBatPath = UECommandsHelper.GetBuildScriptPath(UeDirectoryPath, MyEditorPlatform);

        BuildProjectArgs =
            UECommandsHelper.GetBuildCommandArgs(PluginConfig.ProjectName, ProjectFilePath, MyEditorPlatform);

        RunUatBatPath = UECommandsHelper.GetRunUatScriptPath(UeDirectoryPath, MyEditorPlatform);

        Logger.LogInformation("----------------------------------------------");
        Logger.LogInformation($"Build result directory: {BuildResultDir}");
        Logger.LogInformation($"Test result directory: {TestResultDir}");
        Logger.LogInformation($"Package directory: {PackageDir}");
        Logger.LogInformation($"Game directory: {GameDir}");
        Logger.LogInformation($"Project file path: {ProjectFilePath}");
        Logger.LogInformation($"Unreal Engine directory: {UeDirectoryPath}");
        Logger.LogInformation($"Unreal Editor path: {EditorPath}");
        Logger.LogInformation($"Build script path: {BuildBatPath}");
        Logger.LogInformation($"Build project arguments: {BuildProjectArgs}");
        Logger.LogInformation($"Run UAT script path: {RunUatBatPath}");
        Logger.LogInformation("----------------------------------------------");
    }

    /// <summary>
    /// Gets the game platforms for the builder implementation.
    /// </summary>
    protected abstract List<GamePlatform> MyPlatform();

    /// <summary>
    /// Gets the editor platform for the builder implementation.
    /// </summary>
    protected abstract EditorPlatform MyEditorPlatform { get; }

    /// <summary>
    /// Checks the test result for the specified engine version.
    /// </summary>
    protected BuildStatus CheckTestResult(UEVersion engineVersion)
    {
        return TestResultsEvaluator.EvaluateTestResults(Path.Combine(TestResultDir, "index.json"), MyEditorPlatform, engineVersion);
    }
    
    /// <summary>
    /// Checks the game package build result for the specified engine version.
    /// </summary>
    protected BuildStatus CheckPackageBuildResult(UEVersion engineVersion)
    {
        return TestResultsEvaluator.EvaluatePackageBuildResults(
            Path.Combine(PackageDir, "Plugin", $"{PluginConfig.PluginName}.uplugin"), MyEditorPlatform,
            engineVersion);
    }

    protected static bool IsUatSuccessLog(string logContent)
    {
        return logContent.Contains("AutomationTool exiting with ExitCode=0") ||
               logContent.Contains("BUILD SUCCESSFUL");
    }

    /// <inheritdoc />
    public string GetGameDirectory()
    {
        return GameDir;
    }
}
