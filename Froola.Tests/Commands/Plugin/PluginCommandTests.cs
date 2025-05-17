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
            "TestPlugin", "TestProject", "https://example.com/repo.git", "main", null, string.Empty,
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", true, true, new[] { "Win64" }, null,
            true, false // IsZipped, KeepBinaryDirectory
        },
        // With GitBranches
        new object?[]
        {
            "TestPlugin", "TestProject", "https://example.com/repo.git", "main", 
            new[] { "5.3:UE5.3", "5.2:UE5.2" }, string.Empty,
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", true, true, new[] { "Win64" }, null,
            true, false
        },
        // With GitBranches and empty GitBranch
        new object?[]
        {
            "TestPlugin", "TestProject", "https://example.com/repo.git", "", 
            new[] { "5.3:UE5.3" }, string.Empty,
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", true, true, new[] { "Win64" }, null,
            true, false
        },
        // PluginName is empty (Abnormal case)
        new object?[]
        {
            string.Empty, "TestProject", "https://example.com/repo.git", "main", null, string.Empty,
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", true, true, new[] { "Win64" },
            typeof(ArgumentException),
            true, false
        },
        // GitBranch is empty but has GitBranches (Should NOT throw)
        new object?[]
        {
            "TestPlugin", "TestProject", "https://example.com/repo.git", "", 
            new[] { "5.3:UE5.3" }, string.Empty,
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", true, true, new[] { "Win64" }, null,
            true, false
        },
        // GitBranch is empty and no GitBranches but has LocalRepositoryPath (Should NOT throw)
        new object?[]
        {
            "TestPlugin", "TestProject", "https://example.com/repo.git", "", null, Directory.GetCurrentDirectory(),
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", true, true, new[] { "Win64" }, null,
            false, true
        },
        // gitRepositoryUrl is null (Should NOT throw)
        new object?[]
        {
            "TestPlugin", "TestProject", null, "main", null, Directory.GetCurrentDirectory(),
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", true, true, new[] { "Win64" }, null,
            false, false
        },
        // gitBranch is null (Should NOT throw)
        new object?[]
        {
            "TestPlugin", "TestProject", "https://example.com/repo.git", null, null, Directory.GetCurrentDirectory(),
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", true, true, new[] { "Win64" }, null,
            true, true
        },
        // EditorPlatforms is empty (Abnormal case)
        new object?[]
        {
            "TestPlugin", "TestProject", "https://example.com/repo.git", "main", null, string.Empty,
            new string[] { }, new[] { "5.3" }, "test_outputs", true, true, new[] { "Win64" }, typeof(ArgumentException),
            true, false
        },
        // EngineVersions is empty (Abnormal case)
        new object?[]
        {
            "TestPlugin", "TestProject", "https://example.com/repo.git", "main", null, string.Empty,
            new[] { "Windows" }, new string[] { }, "test_outputs", true, true, new[] { "Win64" },
            typeof(ArgumentException),
            false, false
        },
        // PackagePlatforms is empty (Abnormal case)
        new object?[]
        {
            "TestPlugin", "TestProject", "https://example.com/repo.git", "main", null, string.Empty,
            new[] { "Windows" }, new[] { "5.3" }, "test_outputs", true, true, new string[] { },
            typeof(ArgumentException),
            true, true
        }
    };

    [Theory]
    [MemberData(nameof(RunTestCases))]
    public async Task Run_MergesConfigAndArgs_Variations(
        string pluginName, string projectName, string? gitRepositoryUrl, string? gitBranch, string[]? gitBranches, string? localRepoPath,
        string[] editorPlatforms, string[] engineVersions, string resultPath,
        bool runTest, bool runPackage, string[] packagePlatforms, Type? expectedException,
        bool isZipped, bool keepBinaryDirectory)
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
            PackagePlatforms = new OptionList<GamePlatform>(packagePlatforms.Select(Enum.Parse<GamePlatform>)),
            IsZipped = isZipped,
            KeepBinaryDirectory = keepBinaryDirectory
        };
        var gitConfig = new GitConfig
        {
            GitRepositoryUrl = gitRepositoryUrl ?? string.Empty,
            GitBranch = gitBranch ?? string.Empty,
            GitSshKeyPath = "",
            LocalRepositoryPath = localRepoPath ?? string.Empty
        };

        // Set GitBranches if provided
        if (gitBranches != null && gitBranches.Length > 0)
        {
            foreach (var branch in gitBranches)
            {
                var parts = branch.Split(':');
                if (parts.Length == 2)
                {
                    gitConfig.GitBranches[parts[0]] = parts[1];
                }
            }
        }
        var windowsConfig = new WindowsConfig { WindowsUnrealBasePath = @"C:\Program Files\Epic Games" };
        var macConfig = new MacConfig();
        var linuxConfig = new LinuxConfig();

        var gitMock = new Mock<IGitClient>();
        gitMock.Setup(x => x.CloneRepository(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.FromResult(true));

        var fileSystemMock = new Mock<IFileSystem>();
        fileSystemMock.Setup(x => x.DirectoryExists(Directory.GetCurrentDirectory())).Returns(true);

        var mockContainerBuilder = new Mock<IContainerBuilder>();
        mockContainerBuilder.Setup(x => x.Register(It.IsAny<IServiceCollection>()))
            .Callback<IServiceCollection>(services =>
            {
                services.AddSingleton(typeof(ITestOutputHelper), outputHelper);
                services.AddSingleton<IFroolaLogger, TestFroolaLogger>();
                services.AddSingleton(typeof(IFroolaLogger<>), typeof(FroolaLogger<>));
                services.AddSingleton(typeof(IConfigJsonExporter), new Mock<IConfigJsonExporter>().Object);
                services.AddSingleton(typeof(IGitClient), gitMock.Object);
                services.AddSingleton(typeof(IFileSystem), fileSystemMock.Object);
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
                pluginName,
                projectName,
                gitRepositoryUrl,
                gitBranch,
                gitBranches,
                localRepoPath,
                editorPlatforms,
                engineVersions,
                resultPath,
                runTest,
                runPackage,
                packagePlatforms
            );
            // Assert values after run
            Assert.Equal(isZipped, pluginConfig.IsZipped);
            Assert.Equal(keepBinaryDirectory, pluginConfig.KeepBinaryDirectory);
        }
        else
        {
            await Assert.ThrowsAsync(expectedException, () => command.Run(
                pluginName, projectName, gitRepositoryUrl, gitBranch,
                gitBranches,
                localRepoPath,
                editorPlatforms,
                engineVersions,
                resultPath,
                runTest,
                runPackage,
                packagePlatforms
            ));
            // Assert values even if exception
            Assert.Equal(isZipped, pluginConfig.IsZipped);
            Assert.Equal(keepBinaryDirectory, pluginConfig.KeepBinaryDirectory);
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
            GitSshKeyPath = "",
            LocalRepositoryPath = string.Empty
        };
        var windowsConfig = new WindowsConfig { WindowsUnrealBasePath = @"C:\Program Files\Epic Games" };
        var macConfig = new MacConfig();
        var linuxConfig = new LinuxConfig();

        var gitMock = new Mock<IGitClient>();
        gitMock.Setup(x => x.CloneRepository(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.FromResult(true));
        
        var mockContainerBuilder = new Mock<IContainerBuilder>();
        mockContainerBuilder.Setup(x => x.Register(It.IsAny<IServiceCollection>()))
            .Callback<IServiceCollection>(services =>
            {
                services.AddSingleton(typeof(ITestOutputHelper), outputHelper);
                services.AddSingleton<IFroolaLogger, TestFroolaLogger>();
                services.AddSingleton(typeof(IFroolaLogger<>), typeof(FroolaLogger<>));
                services.AddSingleton(typeof(IConfigJsonExporter), new Mock<IConfigJsonExporter>().Object);
                services.AddSingleton(typeof(IGitClient), gitMock.Object);
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
            null, // gitBranches
            gitConfig.LocalRepositoryPath,
            ["Windows"],
            ["5.3"],
            pluginConfig.ResultPath,    
            pluginConfig.RunTest,
            pluginConfig.RunPackage,
            ["Win64"],
            pluginConfig.KeepBinaryDirectory,
            pluginConfig.IsZipped,
            default
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
            GitSshKeyPath = "",
            LocalRepositoryPath = string.Empty
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
            null,
            gitConfig.LocalRepositoryPath,
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
            GitSshKeyPath = "",
            LocalRepositoryPath = string.Empty
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
            null,
            gitConfig.LocalRepositoryPath,
            ["Windows"],
            ["5.3"],
            pluginConfig.ResultPath,
            pluginConfig.RunTest,
            pluginConfig.RunPackage,
            ["Win64"]
        ));
    }
}