using Froola.Configs.Attributes;

namespace Froola.Tests.TestHelpers;

[OptionClass("Test")]
public class TestConfig
{
    /// <summary>
    ///     Name of the plugin
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    ///     Name of the project
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;
    
    public bool RunTest { get; set; } = false;
}