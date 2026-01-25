using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using Froola.Annotations;
using Froola.Configs;
using Froola.Configs.Collections;
using Froola.Interfaces;
using Microsoft.Extensions.Options;

namespace Froola.Commands.Plugin;

[RegisterCommands]
public class PluginCommand(
    IOptions<PluginConfig> configOptions,
    IOptions<GitConfig> gitOptions,
    IOptions<WindowsConfig> windowsConfig,
    IOptions<MacConfig> macConfig,
    IOptions<LinuxConfig> linuxConfig)
{
    private PluginConfig _pluginConfig = null!;
    private GitConfig _gitConfig = null!;
    private IGitClient _gitClient = null!;
    private IFileSystem _fileSystem = null!;
    private readonly Dictionary<UEVersion, string> _baseRepoPathMap = new();

    private IFroolaLogger<PluginCommand> _logger = null!;

    public IContainerBuilder ContainerBuilder { get; init; } = new PluginContainerBuilder();

    /// <summary>
    ///     Runs the plugin build, test, and packaging process.
    /// </summary>
    /// <param name="pluginName">-n,Name of the plugin</param>
    /// <param name="projectName">-p,Name of the project</param>
    /// <param name="gitRepositoryUrl">-u,URL of the git repository</param>
    /// <param name="gitBranch">-b,Branch of the git repository</param>
    /// <param name="gitBranches">g,Branches of the git repository (format version:branch)</param>
    /// <param name="localRepositoryPath">-l,Path to the local repository</param>
    /// <param name="editorPlatforms">-e,Editor platforms</param>
    /// <param name="engineVersions">-v,Engine versions</param>
    /// <param name="resultPath">-o,Path to save results</param>
    /// <param name="runTest">-t,Run tests</param>
    /// <param name="runPackage">-c,Run Plugin packaging</param>
    /// <param name="runGamePackage">-gp,Run game packaging</param>
    /// <param name="packagePlatforms">-g,Game platforms</param>
    /// <param name="keepBinaryDirectory">-d,Exclude the binary directory.</param>
    /// <param name="isZipped">-z,Create a zip archive of the release directory</param>
    /// <param name="copyPackageAfterBuild">-r,Copy packaged plugin to configure destination paths</param>
    /// <param name="environmentVariables">-i,Environment variables</param>
    /// <param name="cancellationToken">token for cancellation</param>
    [Command("plugin")]
    [SuppressMessage("ReSharper", "ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator")]
    public async Task Run(
        [Required] string pluginName,
        [Required] string projectName,
        string? gitRepositoryUrl = null,
        string? gitBranch = null,
        string[]? gitBranches = null,
        string? localRepositoryPath = null,
        [EnumArray(typeof(EditorPlatform))] string[]? editorPlatforms = null,
        [UeVersionEnumArray] string[]? engineVersions = null,
        string? resultPath = null,
        bool? runTest = null,
        bool? runPackage = null,
        bool? runGamePackage = null,
        [EnumArray(typeof(GamePlatform))] string[]? packagePlatforms = null,
        bool? keepBinaryDirectory = null,
        bool? isZipped = null,
        bool? copyPackageAfterBuild = null,
        string[]? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        _pluginConfig = new PluginConfig
        {
            PluginName = pluginName,
            ProjectName = projectName,
            EditorPlatforms = editorPlatforms is null
                ? configOptions.Value.EditorPlatforms
                : new OptionList<EditorPlatform>(editorPlatforms.Select(x => Enum.Parse<EditorPlatform>(x, true))
                    .ToArray()),
            EngineVersions = engineVersions is null
                ? configOptions.Value.EngineVersions
                : new OptionList<UEVersion>(engineVersions.Select(UEVersionExtensions.Parse).ToArray()),
            ResultPath = resultPath ?? configOptions.Value.ResultPath,
            RunTest = runTest ?? configOptions.Value.RunTest,
            RunPackage = runPackage ?? configOptions.Value.RunPackage,
            RunGamePackage = runGamePackage ?? configOptions.Value.RunGamePackage,
            PackagePlatforms = packagePlatforms is null
                ? configOptions.Value.PackagePlatforms
                : new OptionList<GamePlatform>(
                    packagePlatforms.Select(x => Enum.Parse<GamePlatform>(x, true)).ToArray()),
            KeepBinaryDirectory = keepBinaryDirectory ?? configOptions.Value.KeepBinaryDirectory,
            IsZipped = isZipped ?? configOptions.Value.IsZipped,
            CopyPackageAfterBuild = copyPackageAfterBuild ?? configOptions.Value.CopyPackageAfterBuild,
            EnvironmentVariables = environmentVariables is null
                ? configOptions.Value.EnvironmentVariables
                : new OptionList<string>(environmentVariables)
        }.Build();

        _gitConfig = new GitConfig
        {
            GitRepositoryUrl = gitRepositoryUrl ?? gitOptions.Value.GitRepositoryUrl,
            GitBranch = gitBranch ?? gitOptions.Value.GitBranch,
            GitBranches = gitBranches is null
                ? gitOptions.Value.GitBranches
                : new OptionDictionary(gitBranches.Select(x =>
                {
                    var split = x.Split(':');
                    return split.Length == 2 ? new KeyValuePair<string, string>(split[0], split[1]) : default;
                })),
            GitSshKeyPath = gitOptions.Value.GitSshKeyPath,
            LocalRepositoryPath = localRepositoryPath ?? gitOptions.Value.LocalRepositoryPath
        }.Build();

        var dependencyResolver = new DependencyResolver();
        dependencyResolver.BuildHostWithContainerBuilder(ContainerBuilder, _pluginConfig.ResultPath,
            [_pluginConfig, _gitConfig, windowsConfig.Value, macConfig.Value, linuxConfig.Value]);

        _logger = dependencyResolver.Resolve<IFroolaLogger<PluginCommand>>();

        await OutputSettings(dependencyResolver.Resolve<IConfigJsonExporter>());

        _gitClient = dependencyResolver.Resolve<IGitClient>();
        _gitClient.SshKeyPath = _gitConfig.GitSshKeyPath;

        _fileSystem = dependencyResolver.Resolve<IFileSystem>();

        var repoPath = CreateWorkingRepositoryDirectory();

        var buildResultsMap = new Dictionary<UEVersion, BuildResult[]>();
        foreach (var engineVersion in _pluginConfig.EngineVersions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Stopped plugin tasks due to cancellation.");
                break;
            }

            var baseRepoPath = await CloneGitRepository(engineVersion, repoPath);
            _baseRepoPathMap[engineVersion] = baseRepoPath;

            var tasks = new List<Task<BuildResult>>();
            var builders = dependencyResolver.Resolve<IEnumerable<IBuilder>>().ToArray();

            foreach (var editorPlatform in _pluginConfig.EditorPlatforms)
            {
                var builder = editorPlatform switch
                {
                    EditorPlatform.Windows => builders.FirstOrDefault(x => x is IWindowsBuilder),
                    EditorPlatform.Mac => builders.FirstOrDefault(x => x is IMacBuilder),
                    EditorPlatform.Linux => builders.FirstOrDefault(x => x is ILinuxBuilder),
                    _ => throw new ArgumentException($"Unknown OS: {editorPlatform}")
                };

                if (builder is null)
                {
                    _logger.LogWarning($"No builder found for {editorPlatform}");
                    continue;
                }

                await builder.PrepareRepository(baseRepoPath, engineVersion);
                builder.InitDirectory(engineVersion);
                tasks.Add(builder.Run(engineVersion));
            }

            //Wait for all tests to complete
            var results = await Task.WhenAll(tasks);
            buildResultsMap.Add(engineVersion, results);

            MergePackages(engineVersion);

            if (_pluginConfig.RunGamePackage)
            {
                foreach (var result in results)
                {
                    if (result.StatusOfGamePackage != BuildStatus.Success) continue;

                    var builder = result.Os switch
                    {
                        EditorPlatform.Windows => builders.FirstOrDefault(x => x is IWindowsBuilder),
                        EditorPlatform.Mac => builders.FirstOrDefault(x => x is IMacBuilder),
                        EditorPlatform.Linux => builders.FirstOrDefault(x => x is ILinuxBuilder),
                        _ => throw new ArgumentException($"Unknown OS: {result.Os}")
                    };

                    if (builder is not null)
                    {
                        ZipGamePackage(builder, result.Os, engineVersion);
                    }
                }
            }

            foreach (var builder in builders)
            {
                await builder.CleanupTempDirectory();
            }
        }

        // Clean up temporary directory
        CleanupClonedDirectory(repoPath);

        OutputResults(buildResultsMap);

        // Output overall results
        _logger.LogInformation($"All tasks finished. Check {_pluginConfig.ResultPath} for details.");
    }

    private string CreateWorkingRepositoryDirectory()
    {
        // Create a timestamp folder (year-month-day-hour-minute-second)
        var tempBase = Path.Combine(Path.GetTempPath(), "Froola", _pluginConfig.PluginName);
        if (_fileSystem.DirectoryExists(tempBase))
        {
            try
            {
                _fileSystem.DeleteDirectory(tempBase, true);
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Warning: Could not delete directory {tempBase}: {e.Message}");
            }
        }

        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var repoPath = Path.Combine(tempBase, timestamp);

        // Create a results directory if it doesn't exist
        if (!_fileSystem.DirectoryExists(repoPath))
        {
            _fileSystem.CreateDirectory(repoPath);
        }

        return repoPath;
    }

    /// <summary>
    ///     Prepares temporary Git repositories for each target OS.
    /// </summary>
    private async Task<string> CloneGitRepository(UEVersion ueVersion, string repoPath)
    {
        var baseRepoPath = Path.Combine(repoPath, ueVersion.ToString(), "base");
        
        if (string.IsNullOrWhiteSpace(_gitConfig.LocalRepositoryPath))
        {
            _logger.LogInformation($"Preparing Git repositories from {_gitConfig.GitRepositoryUrl}");

            // First clone for Windows
            if (!await _gitClient.CloneRepository(_gitConfig.GitRepositoryUrl,
                    _gitConfig.GitBranchesWithVersion.GetValueOrDefault(ueVersion, _gitConfig.GitBranch),
                    baseRepoPath))
            {
                _logger.LogError("Failed to clone repository for Windows");
                throw new InvalidOperationException("Failed to clone repository for Windows");
            }

            var gitDirectory = Path.Combine(baseRepoPath, ".git");
            try
            {
                _fileSystem.RemoveReadOnlyAttribute(gitDirectory);
                _fileSystem.DeleteDirectory(gitDirectory, true);
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Warning: Could not delete directory {gitDirectory}: {e.Message}");
            }
        }
        else if (_fileSystem.DirectoryExists(_gitConfig.LocalRepositoryPath))
        {
            _logger.LogInformation($"Using local repository {_gitConfig.LocalRepositoryPath}");
            
            string[] directories = ["Source", "Content", "Config", "Plugins"];
            foreach (var directory in directories)
            {
                _fileSystem.CopyDirectory(Path.Combine(_gitConfig.LocalRepositoryPath, directory),
                    Path.Combine(baseRepoPath, directory));
            }

            string[] files = [$"{_pluginConfig.ProjectName}.uproject"];
            foreach (var file in files)
            {
                _fileSystem.FileCopy(Path.Combine(_gitConfig.LocalRepositoryPath, file),
                    Path.Combine(baseRepoPath, file), true);
            }
        }
        else
        {
            _logger.LogError($"Local repository {_gitConfig.LocalRepositoryPath} does not exist");
            throw new InvalidOperationException("Local repository does not exist");
        }

        return baseRepoPath;
    }

    /// <summary>
    ///     Cleans up temporary directories used during the build process.
    /// </summary>
    private void CleanupClonedDirectory(string repoPath)
    {
        if (!_fileSystem.DirectoryExists(repoPath))
        {
            return;
        }

        _logger.LogInformation($"Cleaning up temporary directory for : {repoPath}");

        try
        {
            _fileSystem.DeleteDirectory(repoPath, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Warning: Failed to remove read-only attributes: {ex.Message}");
        }
    }

    /// <summary>
    ///     Outputs the results of the build process.
    /// </summary>
    private void OutputResults(Dictionary<UEVersion, BuildResult[]> buildResultsMap)
    {
        _logger.LogInformation("-------------------------------------------");

        bool isSuccess = true;
        
        foreach (var (engineVersion, value) in buildResultsMap)
        {
            foreach (var buildResult in value)
            {
                _logger.LogInformation(
                    $"[{engineVersion.ToFullVersionString()} {buildResult.Os}] Build    : {buildResult.StatusOfBuild}");
                _logger.LogInformation(
                    $"[{engineVersion.ToFullVersionString()} {buildResult.Os}] Test    : {buildResult.StatusOfTest}");
                _logger.LogInformation(
                    $"[{engineVersion.ToFullVersionString()} {buildResult.Os}] Plugin Package : {buildResult.StatusOfPackage}");      
                _logger.LogInformation(
                    $"[{engineVersion.ToFullVersionString()} {buildResult.Os}] Game Package : {buildResult.StatusOfGamePackage}");
                
                if (!buildResult.IsSuccess)
                {
                    isSuccess = false;
                }
            }
        }
        
        _logger.LogInformation($"Froola Result : {(isSuccess ? "Success" : "Failed")}");

        _logger.LogInformation("-------------------------------------------");
    }

    /// <summary>
    ///     Merges packages for the specified engine version.
    /// </summary>
    private void MergePackages(UEVersion engineVersion)
    {
        var packageDir = Path.Combine(_pluginConfig.ResultPath, "packages");

        if (!_fileSystem.DirectoryExists(packageDir))
        {
            _logger.LogInformation($"No packages directory found for {engineVersion.ToFullVersionString()}");
            return;
        }

        var mergedDir = Path.Combine(_pluginConfig.ResultPath, "release",
            $"{_pluginConfig.PluginName}_{engineVersion.ToFullVersionString()}");
        if (!_fileSystem.DirectoryExists(mergedDir))
        {
            _fileSystem.CreateDirectory(mergedDir);
        }

        var isFirst = true;

        foreach (var editorPlatform in _pluginConfig.EditorPlatforms)
        {
            var platformPackageDir = Path.Combine(packageDir, $"{editorPlatform}_{engineVersion.ToFullVersionString()}",
                "Plugin");
            var platformBinariesDir = Path.Combine(platformPackageDir, "Binaries");
            var platformIntermediateDir = Path.Combine(platformPackageDir, "Intermediate");

            if (!_fileSystem.DirectoryExists(platformPackageDir) ||
                 !_fileSystem.DirectoryExists(platformBinariesDir) ||
                 !_fileSystem.DirectoryExists(platformIntermediateDir))
            {
                continue;
            }

            if (isFirst)
            {
                _fileSystem.CopyDirectory(platformPackageDir, mergedDir);
                isFirst = false;

                if (!_pluginConfig.KeepBinaryDirectory)
                {
                    _fileSystem.DeleteDirectory(Path.Combine(mergedDir, "Binaries"), true);
                    _fileSystem.DeleteDirectory(Path.Combine(mergedDir, "Intermediate"), true);
                    break;
                }
            }
            else
            {
                _fileSystem.CopyDirectory(platformBinariesDir, Path.Combine(mergedDir, "Binaries"));
                _fileSystem.CopyDirectory(platformIntermediateDir, Path.Combine(mergedDir, "Intermediate"));
            }
        }

        if (ZipDirectory(mergedDir, engineVersion))
        {
            _fileSystem.DeleteDirectory(mergedDir, true);
        }
        
        _logger.LogInformation($"Merged directory created for {engineVersion.ToFullVersionString()} : {mergedDir}");
    }

    private bool ZipDirectory(string sourceDirectory, UEVersion engineVersion)
    {
        if (!_pluginConfig.IsZipped || !_fileSystem.DirectoryExists(sourceDirectory))
        {
            return false;
        }

        var zipFileName =
            $"{_pluginConfig.PluginName}_{GetPluginVersion(sourceDirectory)}_{engineVersion.ToFullVersionString()}.zip";
        var zipFilePath = Path.Combine(_pluginConfig.ResultPath, "release", zipFileName);

        _logger.LogInformation($"Creating zip file: {zipFilePath}");

        if (_fileSystem.FileExists(zipFilePath))
        {
            _fileSystem.DeleteFile(zipFilePath);
        }

        ZipFile.CreateFromDirectory(sourceDirectory, zipFilePath);

        _logger.LogInformation($"Zip file created: {zipFilePath}");

        return true;
    }

    private void ZipGamePackage(IBuilder builder, EditorPlatform editorPlatform, UEVersion engineVersion)
    {
        var gamePackageDir = builder.GetGameDirectory();
        
        if (!_fileSystem.DirectoryExists(gamePackageDir))
        {
            _logger.LogWarning($"Game package directory not found: {gamePackageDir}");
            return;
        }

        var pluginVersion = GetPluginVersion(Path.Combine(_baseRepoPathMap[engineVersion], "Plugins", _pluginConfig.PluginName));
        var platform = editorPlatform switch
        {
            EditorPlatform.Windows => "Win64",
            EditorPlatform.Mac => "Mac",
            EditorPlatform.Linux => "Linux",
            _ => editorPlatform.ToString()
        };

        var zipFileName = $"{_pluginConfig.ProjectName}_{pluginVersion}_{engineVersion.ToFullVersionString()}_{platform}.zip";
        var zipFilePath = Path.Combine(_pluginConfig.ResultPath, "release", zipFileName);

        if (_fileSystem.FileExists(zipFilePath))
        {
            _fileSystem.DeleteFile(zipFilePath);
        }

        _logger.LogInformation($"Zipping game package to {zipFilePath}");
        try
        {
            ZipFile.CreateFromDirectory(gamePackageDir, zipFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to zip game package from {gamePackageDir} to {zipFilePath}", ex);
            throw;
        }
    }

    private string GetPluginVersion(string sourceDirectory)
    {
        var pluginVersionFilePath = Path.Combine(sourceDirectory, $"{_pluginConfig.PluginName}.uplugin");

        if (!_fileSystem.FileExists(pluginVersionFilePath))
        {
            return "0_0_0";
        }

        var jsonText = _fileSystem.ReadAllText(pluginVersionFilePath);
        var jsonObject = JsonDocument.Parse(jsonText);
        var pluginVersion = jsonObject.RootElement.GetProperty("VersionName").GetString() ?? "0.0.0";

        return pluginVersion.Replace('.', '_').Trim();
    }

    private async Task OutputSettings(IConfigJsonExporter configJsonExporter)
    {
        await configJsonExporter.ExportConfigJson(Path.Combine(_pluginConfig.ResultPath, "settings.json"),
            [_pluginConfig, _gitConfig, windowsConfig.Value, macConfig.Value]);
    }
}