using AutoFixture;
using Froola.Configs;
using Froola.Configs.Collections;

namespace Froola.Tests.Configs;

public class PluginConfigTests
{
    private readonly Fixture _fixture = new();

    // Set valid values for properties
    private void SetupValidPluginConfigCollections(PluginConfig config)
    {
        config.EditorPlatforms = new OptionList<EditorPlatform>
            { EditorPlatform.Windows, EditorPlatform.Mac, EditorPlatform.Linux };
        config.EngineVersions = new OptionList<UEVersion>
        {
            UEVersionExtensions.Parse("5.3"), UEVersionExtensions.Parse("5.4"),
            UEVersionExtensions.Parse("5.5")
        };
        config.PackagePlatforms = new OptionList<GamePlatform>
            { GamePlatform.Win64, GamePlatform.Mac, GamePlatform.Linux, GamePlatform.Android, GamePlatform.IOS };
    }

    [Fact]
    public void Build_SetsResultPath_WhenResultPathIsNullOrEmpty()
    {
        var config = _fixture.Build<PluginConfig>()
            .With(x => x.ResultPath, string.Empty)
            .With(x => x.PluginName, "TestPlugin")
            .With(x => x.ProjectName, "TestProject")
            .Create();
        SetupValidPluginConfigCollections(config);

        var built = config.Build();

        Assert.Contains("TestPlugin", built.ResultPath);
        Assert.Contains("outputs", built.ResultPath);
    }

    [Fact]
    public void Build_Throws_WhenPluginNameIsNullOrEmpty()
    {
        var config = _fixture.Build<PluginConfig>()
            .With(x => x.PluginName, string.Empty)
            .With(x => x.ProjectName, "TestProject")
            .Create();
        SetupValidPluginConfigCollections(config);

        Assert.Throws<ArgumentException>(() => config.Build());
    }

    [Fact]
    public void Build_Throws_WhenProjectNameIsNullOrEmpty()
    {
        var config = _fixture.Build<PluginConfig>()
            .With(x => x.PluginName, "TestPlugin")
            .With(x => x.ProjectName, string.Empty)
            .Create();
        SetupValidPluginConfigCollections(config);

        Assert.Throws<ArgumentException>(() => config.Build());
    }

    [Fact]
    public void Build_ParsesEditorPlatforms()
    {
        var config = _fixture.Build<PluginConfig>()
            .With(x => x.PluginName, "TestPlugin")
            .With(x => x.ProjectName, "TestProject")
            .Create();

        SetupValidPluginConfigCollections(config);

        config.EditorPlatforms = new OptionList<EditorPlatform> { EditorPlatform.Windows, EditorPlatform.Mac };


        var built = config.Build();

        Assert.Contains(built.EditorPlatforms, x => x == EditorPlatform.Windows);
        Assert.Contains(built.EditorPlatforms, x => x == EditorPlatform.Mac);
    }

    [Fact]
    public void Build_Throws_WhenEditorPlatformsIsEmpty()
    {
        var config = _fixture.Build<PluginConfig>()
            .With(x => x.PluginName, "TestPlugin")
            .With(x => x.ProjectName, "TestProject")
            .Create();
        SetupValidPluginConfigCollections(config);
        config.EditorPlatforms = new OptionList<EditorPlatform>();


        Assert.Throws<ArgumentException>(() => config.Build());
    }

    [Fact]
    public void Build_ParsesEngineVersions()
    {
        var config = _fixture.Build<PluginConfig>()
            .With(x => x.PluginName, "TestPlugin")
            .With(x => x.ProjectName, "TestProject")
            .Create();
        SetupValidPluginConfigCollections(config);
        config.EngineVersions = new OptionList<UEVersion>
            { UEVersionExtensions.Parse("5.4"), UEVersionExtensions.Parse("5.5") };


        var built = config.Build();

        Assert.True(built.EngineVersions.Count >= 2);
    }

    [Fact]
    public void Build_Throws_WhenEngineVersionsIsEmpty()
    {
        var config = _fixture.Build<PluginConfig>()
            .With(x => x.PluginName, "TestPlugin")
            .With(x => x.ProjectName, "TestProject")
            .Create();
        SetupValidPluginConfigCollections(config);
        config.EngineVersions = new OptionList<UEVersion>();


        Assert.Throws<ArgumentException>(() => config.Build());
    }
}