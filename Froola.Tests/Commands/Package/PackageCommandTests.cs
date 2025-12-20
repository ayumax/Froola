using Froola.Commands.Package;
using Froola.Commands.Plugin;
using Froola.Configs;
using Froola.Configs.Collections;
using Froola.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit.Abstractions;

namespace Froola.Tests.Commands.Package;

public class PackageCommandTests(ITestOutputHelper outputHelper)
{
    public static IEnumerable<object?[]> RunTestCases => new List<object?[]>
    {
        // Normal case
        new object?[]
        {
            "TestProject", "https://example.com/repo.git", "main", null, string.Empty,
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", new[] { "Win64" }, null,
            true // IsZipped
        },
        // ProjectName is empty (Abnormal case)
        new object?[]
        {
            string.Empty, "https://example.com/repo.git", "main", null, string.Empty,
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", new[] { "Win64" },
            typeof(ArgumentException),
            true
        },
        // EditorPlatforms is empty (Abnormal case)
        new object?[]
        {
            "TestProject", "https://example.com/repo.git", "main", null, string.Empty,
            new string[] { }, new[] { "5.3" }, "test_outputs", new[] { "Win64" }, typeof(ArgumentException),
            true
        },
        // EngineVersions is empty (Abnormal case)
        new object?[]
        {
            "TestProject", "https://example.com/repo.git", "main", null, string.Empty,
            new[] { "Windows" }, new string[] { }, "test_outputs", new[] { "Win64" },
            typeof(ArgumentException),
            false
        },
        // PackagePlatforms is empty (Abnormal case)
        new object?[]
        {
            "TestProject", "https://example.com/repo.git", "main", null, string.Empty,
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", new string[] { },
            typeof(ArgumentException),
            true
        }
    };

    [Theory]
    [MemberData(nameof(RunTestCases))]
    public async Task Run_MergesConfigAndArgs_Variations(
        string projectName, string? gitRepositoryUrl, string? gitBranch, string[]? gitBranches, string? localRepoPath,
        string[] editorPlatforms, string[] engineVersions, string resultPath,
        string[] packagePlatforms, Type? expectedException,
        bool isZipped)
    {
        var packageConfig = new PackageConfig
        {
            ProjectName = projectName,
            EditorPlatforms = new OptionList<EditorPlatform>(editorPlatforms.Select(x => Enum.Parse<EditorPlatform>(x, true)).ToArray()),
            EngineVersions = new OptionList<UEVersion>(engineVersions.Select(UEVersionExtensions.Parse).ToArray()),
            ResultPath = resultPath,
            PackagePlatforms = new OptionList<GamePlatform>(packagePlatforms.Select(x => Enum.Parse<GamePlatform>(x, true)).ToArray()),
            IsZipped = isZipped
        };

        var gitConfig = new GitConfig
        {
            GitRepositoryUrl = gitRepositoryUrl ?? string.Empty,
            GitBranch = gitBranch ?? string.Empty,
            GitBranches = gitBranches is null
                ? new OptionDictionary()
                : new OptionDictionary(gitBranches.Select(x =>
                {
                    var split = x.Split(':');
                    return split.Length == 2 ? new KeyValuePair<string, string>(split[0], split[1]) : default;
                })),
            LocalRepositoryPath = localRepoPath ?? string.Empty
        };

        var packageOptions = Mock.Of<IOptions<PackageConfig>>(o => o.Value == packageConfig);
        var gitOptions = Mock.Of<IOptions<GitConfig>>(o => o.Value == gitConfig);
        var windowsOptions = Mock.Of<IOptions<WindowsConfig>>(o => o.Value == new WindowsConfig());
        var macOptions = Mock.Of<IOptions<MacConfig>>(o => o.Value == new MacConfig());
        var linuxOptions = Mock.Of<IOptions<LinuxConfig>>(o => o.Value == new LinuxConfig());

        var command = new PackageCommand(packageOptions, gitOptions, windowsOptions, macOptions, linuxOptions);

        var mockGitClient = new Mock<IGitClient>();
        var mockFileSystem = new Mock<IFileSystem>();
        var mockLogger = new Mock<IFroolaLogger<PackageCommand>>();
        var mockBuilder = new Mock<IBuilder>();
        mockBuilder.As<IWindowsBuilder>(); // Add interface before accessing .Object
        var mockConfigJsonExporter = new Mock<IConfigJsonExporter>();

        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);

        mockBuilder.Setup(x => x.RunPackage(It.IsAny<UEVersion>()))
            .ReturnsAsync(new BuildResult { StatusOfPackage = BuildStatus.Success });

        var containerBuilder = new Mock<IContainerBuilder>();
        containerBuilder.Setup(x => x.Register(It.IsAny<IServiceCollection>()))
            .Callback<IServiceCollection>(services =>
            {
                services.AddSingleton(mockGitClient.Object);
                services.AddSingleton(mockFileSystem.Object);
                services.AddSingleton(mockLogger.Object);
                services.AddSingleton<IFroolaLogger>(mockLogger.Object);
                services.AddSingleton<IBuilder>(mockBuilder.Object);
                services.AddSingleton<IWindowsBuilder>(mockBuilder.As<IWindowsBuilder>().Object);
                services.AddSingleton(mockConfigJsonExporter.Object);
            });

        var packageCommand = new PackageCommand(packageOptions, gitOptions, windowsOptions, macOptions, linuxOptions)
        {
            ContainerBuilder = containerBuilder.Object
        };

        if (expectedException != null)
        {
            await Assert.ThrowsAsync(expectedException, () => packageCommand.Run(
                projectName, gitRepositoryUrl, gitBranch, gitBranches, localRepoPath,
                editorPlatforms, engineVersions, resultPath,
                packagePlatforms, isZipped));
        }
        else
        {
            await packageCommand.Run(
                projectName, gitRepositoryUrl, gitBranch, gitBranches, localRepoPath,
                editorPlatforms, engineVersions, resultPath,
                packagePlatforms, isZipped);

            // Verify builder was called
            mockBuilder.Verify(x => x.RunPackage(It.IsAny<UEVersion>()), Times.Exactly(engineVersions.Length * editorPlatforms.Length));
        }
    }
}
