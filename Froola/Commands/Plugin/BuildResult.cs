namespace Froola.Commands.Plugin;

/// <summary>
///     Status of build or test execution.
/// </summary>
public enum BuildStatus
{
    /// <summary>
    ///     Not executed
    /// </summary>
    None,

    /// <summary>
    ///     Success
    /// </summary>
    Success,

    /// <summary>
    ///     Failed
    /// </summary>
    Failed
}


/// <summary>
/// Test and package result information.
/// </summary>
public struct BuildResult
{
    /// <summary>
    ///     Status of the build.
    /// </summary>
    public BuildStatus StatusOfBuild { get; set; }
    
    /// <summary>
    /// Status of the test execution.
    /// </summary>
    public BuildStatus StatusOfTest { get; set; }

    /// <summary>
    ///     Status of the package build.
    /// </summary>
    public BuildStatus StatusOfPackage { get; set; }

    /// <summary>
    /// Operating system.
    /// </summary>
    public EditorPlatform Os { get; set; }

    /// <summary>
    /// Engine version string.
    /// </summary>
    public UEVersion EngineVersion { get; set; }
}