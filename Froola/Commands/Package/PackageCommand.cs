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
using Froola.Commands.Plugin;
using Froola.Configs;
using Froola.Configs.Collections;
using Froola.Interfaces;
using Microsoft.Extensions.Options;

namespace Froola.Commands.Package;

[RegisterCommands]
public class PackageCommand(
    IOptions<PackageConfig> configOptions,
    IOptions<GitConfig> gitOptions,
    IOptions<WindowsConfig> windowsConfig,
    IOptions<MacConfig> macConfig,
    IOptions<LinuxConfig> linuxConfig)
{
    private PackageConfig _packageConfig = null!;
    private GitConfig _gitConfig = null!;
    private IGitClient _gitClient = null!;
    private IFileSystem _fileSystem = null!;

    private IFroolaLogger<PackageCommand> _logger = null!;

    public IContainerBuilder ContainerBuilder { get; init; } = new PackageContainerBuilder();

    /// <summary>
    ///     Runs the project packaging process.
    /// </summary>
    /// <param name="projectName">-p,Name of the project</param>
    /// <param name="gitRepositoryUrl">-u,URL of the git repository</param>
    /// <param name="gitBranch">-b,Branch of the git repository</param>
    /// <param name="gitBranches">g,Branches of the git repository (format version:branch)</param>
    /// <param name="localRepositoryPath">-l,Path to the local repository</param>
    /// <param name="editorPlatforms">-e,Editor platforms</param>
    /// <param name="engineVersions">-v,Engine versions</param>
    /// <param name="resultPath">-o,Path to save results</param>
    /// <param name="packagePlatforms">-g,Game platforms</param>
    /// <param name="isZipped">-z,Create a zip archive of the release directory</param>
    /// <param name="zipPackageName">Name of the zip package</param>
    /// <param name="environmentVariables">-i,Environment variables</param>
    /// <param name="cancellationToken">token for cancellation</param>
    [Command("package")]
    [SuppressMessage("ReSharper", "ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator")]
    public async Task Run(
        [Required] string projectName,
        string? gitRepositoryUrl = null,
        string? gitBranch = null,
        string[]? gitBranches = null,
        string? localRepositoryPath = null,
        [EnumArray(typeof(EditorPlatform))] string[]? editorPlatforms = null,
        [UeVersionEnumArray] string[]? engineVersions = null,
        string? resultPath = null,
        [EnumArray(typeof(GamePlatform))] string[]? packagePlatforms = null,
        bool? isZipped = null,
        string? zipPackageName = null,
        string[]? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        _packageConfig = new PackageConfig
        {
            ProjectName = projectName,
            EditorPlatforms = editorPlatforms is null
                ? configOptions.Value.EditorPlatforms
                : new OptionList<EditorPlatform>(editorPlatforms.Select(x => Enum.Parse<EditorPlatform>(x, true))
                    .ToArray()),
            EngineVersions = engineVersions is null
                ? configOptions.Value.EngineVersions
                : new OptionList<UEVersion>(engineVersions.Select(UEVersionExtensions.Parse).ToArray()),
            ResultPath = resultPath ?? configOptions.Value.ResultPath,
            PackagePlatforms = packagePlatforms is null
                ? configOptions.Value.PackagePlatforms
                : new OptionList<GamePlatform>(
                    packagePlatforms.Select(x => Enum.Parse<GamePlatform>(x, true)).ToArray()),
            IsZipped = isZipped ?? configOptions.Value.IsZipped,
            ZipPackageName = zipPackageName ?? configOptions.Value.ZipPackageName,
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

        dependencyResolver.BuildHostWithContainerBuilder(ContainerBuilder, _packageConfig.ResultPath,
            [_packageConfig, _gitConfig, windowsConfig.Value, macConfig.Value, linuxConfig.Value]);

        var configs = new object[]
            { _packageConfig, _gitConfig, windowsConfig.Value, macConfig.Value, linuxConfig.Value };

        _gitClient = dependencyResolver.Resolve<IGitClient>();
        _fileSystem = dependencyResolver.Resolve<IFileSystem>();
        _logger = dependencyResolver.Resolve<IFroolaLogger<PackageCommand>>();
        var configJsonExporter = dependencyResolver.Resolve<IConfigJsonExporter>();

        await OutputSettings(configJsonExporter, configs);

        var buildResultsMap = new Dictionary<UEVersion, BuildResult[]>();

        foreach (var engineVersion in _packageConfig.EngineVersions)
        {
            var repoPath = CreateWorkingRepositoryDirectory();

            try
            {
                var sourcePath = await CloneGitRepository(engineVersion, repoPath);

                var builders = dependencyResolver.Resolve<IEnumerable<IBuilder>>().ToArray();

                var results = new List<BuildResult>();

                foreach (var editorPlatform in _packageConfig.EditorPlatforms)
                {
                    var builder = editorPlatform switch
                    {
                        EditorPlatform.Windows => builders.FirstOrDefault(x => x is IWindowsBuilder),
                        EditorPlatform.Mac => builders.FirstOrDefault(x => x is IMacBuilder),
                        EditorPlatform.Linux => builders.FirstOrDefault(x => x is ILinuxBuilder),
                        _ => null
                    };

                    if (builder == null)
                    {
                        _logger.LogWarning($"Builder for platform {editorPlatform} not found.");
                        continue;
                    }

                    builder.InitDirectory(engineVersion);

                    await builder.PrepareRepository(sourcePath, engineVersion);

                    var result = await builder.RunPackage(engineVersion);
                    results.Add(result);

                    await builder.CleanupTempDirectory();
                }

                buildResultsMap.Add(engineVersion, results.ToArray());
            }
            finally
            {
                CleanupClonedDirectory(repoPath);
            }
        }

        OutputResults(buildResultsMap);

        _logger.LogInformation($"All tasks finished. Check {_packageConfig.ResultPath} for details.");
    }

    private string CreateWorkingRepositoryDirectory()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), "Froola", _packageConfig.ProjectName);
        var repoPath = Path.Combine(tempBase, Guid.NewGuid().ToString());
        _fileSystem.CreateDirectory(repoPath);
        return repoPath;
    }

    private async Task<string> CloneGitRepository(UEVersion ueVersion, string repoPath)
    {
        var gitBranch = _gitConfig.GitBranches.GetValueOrDefault(ueVersion.ToVersionString(), _gitConfig.GitBranch);

        if (!string.IsNullOrEmpty(_gitConfig.LocalRepositoryPath))
        {
            _logger.LogInformation($"Using local repository at: {_gitConfig.LocalRepositoryPath}");
            _fileSystem.CopyDirectory(_gitConfig.LocalRepositoryPath, repoPath);
            return repoPath;
        }

        _logger.LogInformation($"Cloning repository: {_gitConfig.GitRepositoryUrl} branch: {gitBranch}");
        await _gitClient.CloneRepository(_gitConfig.GitRepositoryUrl, gitBranch, repoPath);

        return repoPath;
    }

    private void CleanupClonedDirectory(string repoPath)
    {
        try
        {
            if (_fileSystem.DirectoryExists(repoPath))
            {
                _fileSystem.DeleteDirectory(repoPath, true);
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning($"Failed to cleanup directory {repoPath}: {e.Message}");
        }
    }

    private void OutputResults(Dictionary<UEVersion, BuildResult[]> buildResultsMap)
    {
        _logger.LogInformation("==============================================");
        _logger.LogInformation("FINAL RESULTS");
        _logger.LogInformation("==============================================");

        foreach (var (version, results) in buildResultsMap)
        {
            _logger.LogInformation($"Unreal Engine {version.ToFullVersionString()}:");
            foreach (var result in results)
            {
                _logger.LogInformation(
                    $"  - {result.Os}: Package: {result.StatusOfPackage}");
            }
        }

        _logger.LogInformation("==============================================");
    }

    private async Task OutputSettings(IConfigJsonExporter configJsonExporter, object[] configs)
    {
        var settingsPath = Path.Combine(_packageConfig.ResultPath, "settings.json");
        await configJsonExporter.ExportConfigJson(settingsPath, configs);
        _logger.LogInformation($"Settings exported to: {settingsPath}");
    }
}
