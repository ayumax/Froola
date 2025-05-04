using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
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

    private string _baseRepoPath = string.Empty;

    private IFroolaLogger<PluginCommand> _logger = null!;

    public IContainerBuilder ContainerBuilder { get; set; } = new PluginContainerBuilder();
    
    /// <summary>
    ///     Runs the plugin build, test, and packaging process.
    /// </summary>
    /// <param name="pluginName">-n,Name of the plugin</param>
    /// <param name="projectName">-p,Name of the project</param>
    /// <param name="gitRepositoryUrl">-u,URL of the git repository</param>
    /// <param name="gitBranch">-b,Branch of the git repository</param>
    /// <param name="editorPlatforms">-e,Editor platforms</param>
    /// <param name="engineVersions">-v,Engine versions</param>
    /// <param name="resultPath">-o,Path to save results</param>
    /// <param name="runTest">-t,Run tests</param>
    /// <param name="runPackage">-c,Run packaging</param>
    /// <param name="packagePlatforms">-g,Game platforms</param>
    /// <param name="cancellationToken">token for cancellation</param>
    [Command("plugin")]
    [SuppressMessage("ReSharper", "ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator")]
    public async Task Run(
        [Required] string pluginName,
        [Required] string projectName,
        [Required] string gitRepositoryUrl,
        [MinLength(1)] [Required] string gitBranch,
        [EnumArray(typeof(EditorPlatform))] string[]? editorPlatforms = null,
        [EnumArray(typeof(UEVersion))] string[]? engineVersions = null,
        string? resultPath = null,
        bool? runTest = null,
        bool? runPackage = null,
        [EnumArray(typeof(GamePlatform))] string[]? packagePlatforms = null,
        CancellationToken cancellationToken = default)
    {
        _pluginConfig = new PluginConfig
        {
            PluginName = pluginName,
            ProjectName = projectName,
            EditorPlatforms = editorPlatforms is null
                ? configOptions.Value.EditorPlatforms
                : new OptionList<EditorPlatform>(editorPlatforms.Select(Enum.Parse<EditorPlatform>).ToArray()),
            EngineVersions = engineVersions is null
                ? configOptions.Value.EngineVersions
                : new OptionList<UEVersion>(engineVersions.Select(UEVersionExtensions.Parse).ToArray()),
            ResultPath = resultPath ?? configOptions.Value.ResultPath,
            RunTest = runTest ?? configOptions.Value.RunTest,
            RunPackage = runPackage ?? configOptions.Value.RunPackage,
            PackagePlatforms = packagePlatforms is null
                ? configOptions.Value.PackagePlatforms
                : new OptionList<GamePlatform>(packagePlatforms.Select(Enum.Parse<GamePlatform>).ToArray())
        }.Build();

        _gitConfig = new GitConfig
        {
            GitRepositoryUrl = gitRepositoryUrl,
            GitBranch = gitBranch,
            GitSshKeyPath = gitOptions.Value.GitSshKeyPath
        }.Build();
        
        var dependencyResolver = new DependencyResolver();
        dependencyResolver.BuildHostWithContainerBuilder(ContainerBuilder, _pluginConfig.ResultPath,
            [_pluginConfig, _gitConfig, windowsConfig.Value, macConfig.Value, linuxConfig.Value]);

        _logger = dependencyResolver.Resolve<IFroolaLogger<PluginCommand>>();

        await OutputSettings(dependencyResolver.Resolve<IConfigJsonExporter>());

        _gitClient = dependencyResolver.Resolve<IGitClient>();
        _gitClient.SshKeyPath = _gitConfig.GitSshKeyPath;

        _fileSystem = dependencyResolver.Resolve<IFileSystem>();

        CreateWorkingRepositoryDirectory();

        await CloneGitRepository();

        var buildResultsMap = new Dictionary<UEVersion, BuildResult[]>();
        foreach (var engineVersion in _pluginConfig.EngineVersions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Stopped plugin tasks due to cancellation.");
                break;
            }

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
                await builder.PrepareRepository(_baseRepoPath, engineVersion);
                builder.InitDirectory(engineVersion);
                tasks.Add(builder.Run(engineVersion));
            }

            //Wait for all tests to complete
            var results = await Task.WhenAll(tasks);
            buildResultsMap.Add(engineVersion, results);

            MergePackages(engineVersion);

            foreach (var builder in builders)
            {
                await builder.CleanupTempDirectory();
            }
        }

        // Clean up temporary directory
        CleanupClonedDirectory();

        // Output overall results
        _logger.LogInformation($"All build finished. Check {_pluginConfig.ResultPath} for details.");

        OutputResults(buildResultsMap);

        _logger.LogInformation("All tasks have completed");
    }

    private void CreateWorkingRepositoryDirectory()
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
        _baseRepoPath = Path.Combine(tempBase, timestamp);

        // Create a results directory if it doesn't exist
        if (!_fileSystem.DirectoryExists(_baseRepoPath))
        {
            _fileSystem.CreateDirectory(_baseRepoPath);
        }
    }

    /// <summary>
    ///     Prepares temporary Git repositories for each target OS.
    /// </summary>
    private async Task CloneGitRepository()
    {
        _logger.LogInformation($"Preparing Git repositories from {_gitConfig.GitRepositoryUrl}");

        // First clone for Windows
        if (!await _gitClient.CloneRepository(_gitConfig.GitRepositoryUrl, _gitConfig.GitBranch,
                _baseRepoPath))
        {
            _logger.LogError("Failed to clone repository for Windows");
        }
    }


    /// <summary>
    ///     Cleans up temporary directories used during the build process.
    /// </summary>
    private void CleanupClonedDirectory()
    {
        if (!_fileSystem.DirectoryExists(_baseRepoPath))
        {
            return;
        }

        _logger.LogInformation($"Cleaning up temporary directory for : {_baseRepoPath}");

        try
        {
            _fileSystem.DeleteDirectory(_baseRepoPath, true);
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
        foreach (var (engineVersion, value) in buildResultsMap)
        {
            foreach (var buildResult in value)
            {
                _logger.LogInformation(
                    $"[{engineVersion.ToFullVersionString()} {buildResult.Os}] Test    : {buildResult.StatusOfTest}");
                _logger.LogInformation(
                    $"[{engineVersion.ToFullVersionString()} {buildResult.Os}] Package : {buildResult.StatusOfPackage}");
            }
        }
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

            if (!_fileSystem.DirectoryExists(platformBinariesDir) ||
                !_fileSystem.DirectoryExists(platformIntermediateDir))
            {
                continue;
            }

            if (isFirst)
            {
                _fileSystem.CopyDirectory(platformPackageDir, mergedDir);
                isFirst = false;
            }
            else
            {
                _fileSystem.CopyDirectory(platformBinariesDir, Path.Combine(mergedDir, "Binaries"));
                _fileSystem.CopyDirectory(platformIntermediateDir, Path.Combine(mergedDir, "Intermediate"));
            }
        }

        _logger.LogInformation($"Merged directory created for {engineVersion.ToFullVersionString()} : {mergedDir}");
    }

    private async Task OutputSettings(IConfigJsonExporter configJsonExporter)
    {
        await configJsonExporter.ExportConfigJson(Path.Combine(_pluginConfig.ResultPath, "settings.json"),
            [_pluginConfig, _gitConfig, windowsConfig.Value, macConfig.Value]);
    }
}