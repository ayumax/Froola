using System.Text.Json.Serialization;
using Froola.Configs.Attributes;

namespace Froola.Configs;

[OptionClass("InitConfig")]
public class InitConfigConfig
{
    public string OutputPath { get; set; } = "";            
}