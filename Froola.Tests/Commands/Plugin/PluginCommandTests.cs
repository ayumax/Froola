using Froola.Commands.Plugin;
using Froola.Configs;
using Froola.Configs.Collections;
using Froola.Interfaces;
using Froola.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit.Abstractions;

namespace Froola.Tests.Commands.Plugin;

public class PluginCommandTests(ITestOutputHelper outputHelper)
{
    public static IEnumerable<object?[]> RunTestCases => new List<object?[]>
    {
        // Normal case
        new object?[]
        {
            "TestPlugin", "TestProject", "https://example.com/repo.git", "main",
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", true, true, new[] { "Win64" }, null
        },
        // PluginName is empty (Abnormal case)
        new object[]
        {
            "", "TestProject", "https://example.com/repo.git", "main",
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", true, true, new[] { "Win64" },
            typeof(ArgumentException)
        },
        // GitBranch is empty (Abnormal case)
        new object[]
        {
            "TestPlugin", "TestProject", "https://example.com/repo.git", "",
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", true, true, new[] { "Win64" },
            typeof(ArgumentException)
        },
        // EditorPlatforms is empty (Abnormal case)
        new object[]
        {
            "TestPlugin", "TestProject", "https://example.com/repo.git", "main",
            Array.Empty<string>(), new[] { "5.3" }, "test_outputs", true, true, new[] { "Win64" },
            typeof(ArgumentException)
        },
        // EngineVersions is empty (Abnormal case)
        new object[]
        {
            "TestPlugin", "TestProject", "https://example.com/repo.git", "main",
            new[] { "Windows" }, Array.Empty<string>(), "test_outputs", true, true, new[] { "Win64" },
            typeof(ArgumentException)
        },
        // PackagePlatforms is empty (Abnormal case)
        new object[]
        {
            "TestPlugin", "TestProject", "https://example.com/repo.git", "main",
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", true, true, Array.Empty<string>(),
            typeof(ArgumentException)
        }
    };

    [Theory]
    [MemberData(nameof(RunTestCases))]
    public async Task Run_MergesConfigAndArgs_Variations(
        string pluginName, string projectName, string gitRepositoryUrl, string gitBranch,
        string[] editorPlatforms, string[] engineVersions, string resultPath,
        bool runTest, bool runPackage, string[] packagePlatforms, Type? expectedException)
    {
        var pluginConfig = new PluginConfig
        {
            PluginName = pluginName,
            ProjectName = projectName,
            EditorPlatforms = new OptionList<EditorPlatform>(editorPlatforms.Select(Enum.Parse<EditorPlatform>)),
            EngineVersions = new OptionList<UEVersion>(engineVersions.Select(UEVersionExtensions.Parse)),
            ResultPath = resultPath,
            RunTest = runTest,
            RunPackage = runPackage,
            PackagePlatforms = new OptionList<GamePlatform>(packagePlatforms.Select(Enum.Parse<GamePlatform>))
        };
        var gitConfig = new GitConfig
        {
            GitRepositoryUrl = gitRepositoryUrl,
            GitBranch = gitBranch,
            GitSshKeyPath = ""
        };
        var windowsConfig = new WindowsConfig { WindowsUnrealBasePath = @"C:\Program Files\Epic Games" };
        var macConfig = new MacConfig();
var linuxConfig = new LinuxConfig();

        var mockContainerBuilder = new Mock<IContainerBuilder>();
        mockContainerBuilder.Setup(x => x.Register(It.IsAny<IServiceCollection>()))
            .Callback<IServiceCollection>(services =>
            {
                services.AddSingleton(typeof(ITestOutputHelper), outputHelper);
                services.AddSingleton<IFroolaLogger, TestFroolaLogger>();
                services.AddSingleton(typeof(IFroolaLogger<>), typeof(FroolaLogger<>));
                services.AddSingleton(typeof(IConfigJsonExporter), new Mock<IConfigJsonExporter>().Object);
                services.AddSingleton(typeof(IGitClient), new Mock<IGitClient>().Object);
                services.AddSingleton(typeof(IFileSystem), new Mock<IFileSystem>().Object);
            });

        var pluginOptions = Mock.Of<IOptions<PluginConfig>>(o => o.Value == pluginConfig);
        var gitOptions = Mock.Of<IOptions<GitConfig>>(o => o.Value == gitConfig);
        var windowsOptions = Mock.Of<IOptions<WindowsConfig>>(o => o.Value == windowsConfig);
        var macOptions = Mock.Of<IOptions<MacConfig>>(o => o.Value == macConfig);

        var linuxOptions = Mock.Of<IOptions<LinuxConfig>>(o => o.Value == linuxConfig);
var command = new PluginCommand(pluginOptions, gitOptions, windowsOptions, macOptions, linuxOptions)
        {
            ContainerBuilder = mockContainerBuilder.Object
        };

        if (expectedException == null)
        {
            await command.Run(
                pluginName, projectName, gitRepositoryUrl, gitBranch,
                editorPlatforms, engineVersions, resultPath, runTest, runPackage, packagePlatforms
            );
        }
        else
        {
            await Assert.ThrowsAsync(expectedException, () => command.Run(
                pluginName, projectName, gitRepositoryUrl, gitBranch,
                editorPlatforms, engineVersions, resultPath, runTest, runPackage, packagePlatforms
            ));
        }
    }

    [Fact]
    public async Task Run_MergesConfigAndArgs_CallsBuild_NoException()
    {
        // Normal case: The values of the arguments and IOptions<T> are correctly merged and no exception occurs in Build()
        var pluginConfig = new PluginConfig
        {
            PluginName = "TestPlugin",
            ProjectName = "TestProject",
            EditorPlatforms = [EditorPlatform.Windows],
            EngineVersions = [UEVersion.UE_5_3],
            ResultPath = "test_outputs",
            RunTest = true,
            RunPackage = true,
            PackagePlatforms = [GamePlatform.Win64]
        };
        var gitConfig = new GitConfig
        {
            GitRepositoryUrl = "https://example.com/repo.git",
            GitBranch = "main",
            GitSshKeyPath = ""
        };
        var windowsConfig = new WindowsConfig { WindowsUnrealBasePath = @"C:\Program Files\Epic Games" };
        var macConfig = new MacConfig();
var linuxConfig = new LinuxConfig();

        var mockContainerBuilder = new Mock<IContainerBuilder>();
        mockContainerBuilder.Setup(x => x.Register(It.IsAny<IServiceCollection>()))
            .Callback<IServiceCollection>(services =>
            {
                services.AddSingleton(typeof(ITestOutputHelper), outputHelper);
                services.AddSingleton<IFroolaLogger, TestFroolaLogger>();
                services.AddSingleton(typeof(IFroolaLogger<>), typeof(FroolaLogger<>));
                services.AddSingleton(typeof(IConfigJsonExporter), new Mock<IConfigJsonExporter>().Object);
                services.AddSingleton(typeof(IGitClient), new Mock<IGitClient>().Object);
                services.AddSingleton(typeof(IFileSystem), new Mock<IFileSystem>().Object);
            });

        var pluginOptions = Mock.Of<IOptions<PluginConfig>>(o => o.Value == pluginConfig);
        var gitOptions = Mock.Of<IOptions<GitConfig>>(o => o.Value == gitConfig);
        var windowsOptions = Mock.Of<IOptions<WindowsConfig>>(o => o.Value == windowsConfig);
        var macOptions = Mock.Of<IOptions<MacConfig>>(o => o.Value == macConfig);

        var linuxOptions = Mock.Of<IOptions<LinuxConfig>>(o => o.Value == linuxConfig);
var command = new PluginCommand(pluginOptions, gitOptions, windowsOptions, macOptions, linuxOptions)
        {
            ContainerBuilder = mockContainerBuilder.Object
        };

        // Execution: No exception occurs
        await command.Run(
            pluginConfig.PluginName,
            pluginConfig.ProjectName,
            gitConfig.GitRepositoryUrl,
            gitConfig.GitBranch,
            ["Windows"],
            ["5.3"],
            pluginConfig.ResultPath,
            pluginConfig.RunTest,
            pluginConfig.RunPackage,
            ["Win64"]
        );
    }

    [Fact]
    public async Task Run_InvalidPluginName_ThrowsException()
    {
        // Abnormal case: PluginName is empty → Exception occurs in Build()
        var pluginConfig = new PluginConfig
        {
            PluginName = "", // 不正
            ProjectName = "TestProject",
            EditorPlatforms = [EditorPlatform.Windows],
            EngineVersions = [UEVersion.UE_5_3],
            ResultPath = "test_outputs",
            RunTest = true,
            RunPackage = true,
            PackagePlatforms = [GamePlatform.Win64]
        };
        var gitConfig = new GitConfig
        {
            GitRepositoryUrl = "https://example.com/repo.git",
            GitBranch = "main",
            GitSshKeyPath = ""
        };
        var windowsConfig = new WindowsConfig { WindowsUnrealBasePath = @"C:\Program Files\Epic Games" };
        var macConfig = new MacConfig();
var linuxConfig = new LinuxConfig();

        var mockContainerBuilder = new Mock<IContainerBuilder>();
        mockContainerBuilder.Setup(x => x.Register(It.IsAny<IServiceCollection>()))
            .Callback<IServiceCollection>(services =>
            {
                services.AddSingleton(typeof(ITestOutputHelper), outputHelper);
                services.AddSingleton<IFroolaLogger, TestFroolaLogger>();
                services.AddSingleton(typeof(IFroolaLogger<>), typeof(FroolaLogger<>));
                services.AddSingleton(typeof(IConfigJsonExporter), new Mock<IConfigJsonExporter>().Object);
                services.AddSingleton(typeof(IGitClient), new Mock<IGitClient>().Object);
                services.AddSingleton(typeof(IFileSystem), new Mock<IFileSystem>().Object);
            });

        var pluginOptions = Mock.Of<IOptions<PluginConfig>>(o => o.Value == pluginConfig);
        var gitOptions = Mock.Of<IOptions<GitConfig>>(o => o.Value == gitConfig);
        var windowsOptions = Mock.Of<IOptions<WindowsConfig>>(o => o.Value == windowsConfig);
        var macOptions = Mock.Of<IOptions<MacConfig>>(o => o.Value == macConfig);

        var linuxOptions = Mock.Of<IOptions<LinuxConfig>>(o => o.Value == linuxConfig);
var command = new PluginCommand(pluginOptions, gitOptions, windowsOptions, macOptions, linuxOptions)
        {
            ContainerBuilder = mockContainerBuilder.Object
        };

        // Execution: Exception occurs
        await Assert.ThrowsAsync<ArgumentException>(() => command.Run(
            pluginConfig.PluginName,
            pluginConfig.ProjectName,
            gitConfig.GitRepositoryUrl,
            gitConfig.GitBranch,
            ["Windows"],
            ["5.3"],
            pluginConfig.ResultPath,
            pluginConfig.RunTest,
            pluginConfig.RunPackage,
            ["Win64"]
        ));
    }

    [Fact]
    public async Task Run_InvalidGitBranch_ThrowsException()
    {
        // Abnormal case: GitBranch is empty → Exception occurs in Build()
        var pluginConfig = new PluginConfig
        {
            PluginName = "TestPlugin",
            ProjectName = "TestProject",
            EditorPlatforms = [EditorPlatform.Windows],
            EngineVersions = [UEVersion.UE_5_3],
            ResultPath = "test_outputs",
            RunTest = true,
            RunPackage = true,
            PackagePlatforms = [GamePlatform.Win64]
        };
        var gitConfig = new GitConfig
        {
            GitRepositoryUrl = "https://example.com/repo.git",
            GitBranch = "", // Abnormal case
            GitSshKeyPath = ""
        };
        var windowsConfig = new WindowsConfig { WindowsUnrealBasePath = @"C:\Program Files\Epic Games" };
        var macConfig = new MacConfig();
var linuxConfig = new LinuxConfig();

        var mockContainerBuilder = new Mock<IContainerBuilder>();
        mockContainerBuilder.Setup(x => x.Register(It.IsAny<IServiceCollection>()))
            .Callback<IServiceCollection>(services =>
            {
                services.AddSingleton(typeof(ITestOutputHelper), outputHelper);
                services.AddSingleton<IFroolaLogger, TestFroolaLogger>();
                services.AddSingleton(typeof(IFroolaLogger<>), typeof(FroolaLogger<>));
                services.AddSingleton(typeof(IConfigJsonExporter), new Mock<IConfigJsonExporter>().Object);
                services.AddSingleton(typeof(IGitClient), new Mock<IGitClient>().Object);
                services.AddSingleton(typeof(IFileSystem), new Mock<IFileSystem>().Object);
            });

        var pluginOptions = Mock.Of<IOptions<PluginConfig>>(o => o.Value == pluginConfig);
        var gitOptions = Mock.Of<IOptions<GitConfig>>(o => o.Value == gitConfig);
        var windowsOptions = Mock.Of<IOptions<WindowsConfig>>(o => o.Value == windowsConfig);
        var macOptions = Mock.Of<IOptions<MacConfig>>(o => o.Value == macConfig);

        var linuxOptions = Mock.Of<IOptions<LinuxConfig>>(o => o.Value == linuxConfig);
var command = new PluginCommand(pluginOptions, gitOptions, windowsOptions, macOptions, linuxOptions)
        {
            ContainerBuilder = mockContainerBuilder.Object
        };

        // Execution: Exception occurs
        await Assert.ThrowsAsync<ArgumentException>(() => command.Run(
            pluginConfig.PluginName,
            pluginConfig.ProjectName,
            gitConfig.GitRepositoryUrl,
            gitConfig.GitBranch,
            ["Windows"],
            ["5.3"],
            pluginConfig.ResultPath,
            pluginConfig.RunTest,
            pluginConfig.RunPackage,
            ["Win64"]
        ));
    }
}