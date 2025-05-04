using System.Text.Json;
using System.Text.Json.Serialization;
using AutoFixture;
using AutoFixture.AutoMoq;
using Froola.Configs;
using Froola.Configs.Collections;
using Froola.Tests.TestHelpers;
using Xunit.Abstractions;
using Fixture = AutoFixture.Fixture;

namespace Froola.Tests;

[Collection(nameof(FileExclusiveCollection))]
public class AppConfigFileTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly Fixture _fixture;

    private static string appsettingsJsonPath => Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

    public AppConfigFileTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;

        _fixture = new Fixture();
        _fixture.Customize(new AutoMoqCustomization());
        _fixture.Customize<MacConfig>(c => c
            .With(x => x.XcodeNames, new OptionDictionary<UEVersion, string>
            {
                [UEVersion.UE_5_5] = _fixture.Create<string>(),
                [UEVersion.UE_5_4] = _fixture.Create<string>()
            }));
        _fixture.Customize<PluginConfig>(c => c
            .With(x => x.RunTest, true)
            .With(x => x.RunPackage, false)
            .With(x => x.EditorPlatforms, [EditorPlatform.Linux, EditorPlatform.Windows])
            .With(x => x.EngineVersions, [UEVersion.UE_5_3, UEVersion.UE_5_5])
            .With(x => x.PackagePlatforms, [GamePlatform.Android, GamePlatform.Mac]));
        _fixture.Customize<WindowsConfig>(c => c
            .With(x => x.WindowsUnrealBasePath, Directory.GetCurrentDirectory()));
    }

    public static IEnumerable<object[]> AllConfigTypes
    {
        get
        {
            var types = ConfigHelper.GetAllConfigTypes();

            List<object[]> ret = [];
            ret.AddRange(types.Select(configType => (object[]) [new[] { configType }]));

            return ret;
        }
    }

    [Theory]
    [MemberData(nameof(AllConfigTypes))]
    public async Task CanLoadConfigFromAppSettingsJson(Type[] configTypes)
    {
        var jsonObj = new Dictionary<string, object?>();

        // Arrange
        foreach (var configType in configTypes)
        {
            var configValue = _fixture.CreateWithType(configType);
            var section = ConfigHelper.GetSection(configType);

            jsonObj[section] = configValue;
        }
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(jsonObj, options);

        await File.WriteAllTextAsync(appsettingsJsonPath, json);

        try
        {
            // Act:
            var dependencyResolver = new DependencyResolver();
            using var host = dependencyResolver.BuildHost(ConfigHelper.GetAllConfigTypes());

            // Assert
            foreach (var configType in configTypes)
            {
                var optionsType = ConfigHelper.GetIOptionsType(configType);
                var optionsInstance = dependencyResolver.Resolve(optionsType);
                var valueProp = ConfigHelper.GetIOptionsValuePropertyInfo(configType);
                var loadedConfig = valueProp.GetValue(optionsInstance);
                foreach (var prop in configType.GetProperties())
                {
                    var expected = prop.GetValue(jsonObj[ConfigHelper.GetSection(configType)]);
                    var actual = prop.GetValue(loadedConfig);
                    Assert.Equal(expected, actual);
                }
            }
        }
        finally
        {
            // テスト後にファイル削除
            if (File.Exists(appsettingsJsonPath))
            {
                File.Delete(appsettingsJsonPath);
            }
        }
    }

    [CollectionDefinition("FileExclusiveCollection")]
public class FileExclusiveCollection : ICollectionFixture<object>
{
}
}