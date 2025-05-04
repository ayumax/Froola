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
public abstract class BuilderBase(
    PluginConfig pluginConfig,
    WindowsConfig windowsConfig,
    MacConfig macConfig,
    IFroolaLogger logger,
    IFileSystem fileSystem,
    ITestResultsEvaluator testResultsEvaluator)
{
    public string RepositoryPath { get; protected set; } = null!;

    protected string BuildResultDir = null!;
    protected string TestResultDir = null!;
    protected string PackageDir = null!;
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
        BuildResultDir = Path.Combine(pluginConfig.ResultPath, "build",
            $"{MyEditorPlatform}_{engineVersion.ToFullVersionString()}");
        if (!fileSystem.DirectoryExists(BuildResultDir))
        {
            fileSystem.CreateDirectory(BuildResultDir);
        }

        TestResultDir = Path.Combine(pluginConfig.ResultPath, "tests",
            $"{MyEditorPlatform}_{engineVersion.ToFullVersionString()}");
        if (!fileSystem.DirectoryExists(TestResultDir))
        {
            fileSystem.CreateDirectory(TestResultDir);
        }

        PackageDir = Path.Combine(pluginConfig.ResultPath, "packages",
            $"{MyEditorPlatform}_{engineVersion.ToFullVersionString()}");
        if (!fileSystem.DirectoryExists(PackageDir))
        {
            fileSystem.CreateDirectory(PackageDir);
        }

        ProjectFilePath =
            UECommandsHelper.GetUprojectPath(RepositoryPath, pluginConfig.ProjectName, MyEditorPlatform);

        UeDirectoryPath =
            UECommandsHelper.GetUeDirectoryPath(windowsConfig, macConfig, engineVersion, MyEditorPlatform);

        EditorPath = UECommandsHelper.GetUnrealEditorPath(UeDirectoryPath, MyEditorPlatform);

        BuildBatPath = UECommandsHelper.GetBuildScriptPath(UeDirectoryPath, MyEditorPlatform);

        BuildProjectArgs =
            UECommandsHelper.GetBuildCommandArgs(pluginConfig.ProjectName, ProjectFilePath, MyEditorPlatform);

        RunUatBatPath = UECommandsHelper.GetRunUatScriptPath(UeDirectoryPath, MyEditorPlatform);

        logger.LogInformation("----------------------------------------------");
        logger.LogInformation($"Build result directory: {BuildResultDir}");
        logger.LogInformation($"Test result directory: {TestResultDir}");
        logger.LogInformation($"Package directory: {PackageDir}");
        logger.LogInformation($"Project file path: {ProjectFilePath}");
        logger.LogInformation($"Unreal Engine directory: {UeDirectoryPath}");
        logger.LogInformation($"Unreal Editor path: {EditorPath}");
        logger.LogInformation($"Build script path: {BuildBatPath}");
        logger.LogInformation($"Build project arguments: {BuildProjectArgs}");
        logger.LogInformation($"Run UAT script path: {RunUatBatPath}");
        logger.LogInformation("----------------------------------------------");
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
        return testResultsEvaluator.EvaluateTestResults(Path.Combine(TestResultDir, "index.json"), MyEditorPlatform, engineVersion);
    }
    
    /// <summary>
    /// Checks the package build result for the specified engine version.
    /// </summary>
    protected BuildStatus CheckPackageBuildResult(UEVersion engineVersion)
    {
        return testResultsEvaluator.EvaluatePackageBuildResults(
            Path.Combine(PackageDir, "Plugin", $"{pluginConfig.PluginName}.uplugin"), MyEditorPlatform,
            engineVersion);
    }
}