using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ConsoleAppFramework;
using Froola.Configs;
using Microsoft.Extensions.Options;

namespace Froola.Commands;

[RegisterCommands]
public class InitConfigCommand(IOptions<InitConfigConfig> configOptions)
{
    private readonly InitConfigConfig _config = configOptions.Value;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Export config template to JSON file
    /// </summary>
    /// <param name="output">-o,Path to save config template</param>
    /// <returns>Task</returns>
    [Command("init-config")]
    public Task Run(string output)
    {
        var outputPath = string.IsNullOrWhiteSpace(output)
            ? configOptions.Value.OutputPath : output;

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        }
        else
        {
            if (Directory.Exists(outputPath) || outputPath.EndsWith("/") || outputPath.EndsWith("\\"))
            {
                outputPath = Path.Combine(outputPath, "appsettings.json");
            }
        }

        var outputDir = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(outputDir))
        {
            outputDir = Directory.GetCurrentDirectory();
        }
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var configTypes = ConfigHelper.GetAllConfigTypes();
        Dictionary<string, object> plugins = new();

        foreach (var configType in configTypes)
        {
            var config = Activator.CreateInstance(configType);
            if (config is null)
            {
                throw new InvalidOperationException($"Failed to create instance of {configType.Name}");
            }

            plugins.Add(ConfigHelper.GetSection(configType), config);
        }

        // Serialize to JSON with indented formatting
        var json = JsonSerializer.Serialize(plugins, _jsonSerializerOptions);

        // Write JSON to file
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"Config template generated: {Path.GetFullPath(outputPath)}");

        return Task.CompletedTask;
    }
}