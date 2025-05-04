using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Froola.Configs;
using Froola.Configs.Converters;
using Froola.Interfaces;

namespace Froola.Runners;

public class ConfigJsonExporter(IFileSystem fileSystem) : IConfigJsonExporter
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter_UEVersion(),
            new JsonStringEnumConverter()
        }
    };


    public async Task ExportConfigJson(string path, object[] configs)
    {
        var outputDir = Path.GetDirectoryName(path)!;
        if (!fileSystem.DirectoryExists(outputDir))
        {
            fileSystem.CreateDirectory(outputDir);
        }

        var allConfigs = configs.ToDictionary(configInstance => ConfigHelper.GetSection(configInstance.GetType()));

        await using var stream = fileSystem.Create(path);
        await JsonSerializer.SerializeAsync(stream, allConfigs, _jsonSerializerOptions);
    }
}