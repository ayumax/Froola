using System.Threading.Tasks;

namespace Froola.Interfaces;

/// <summary>
/// Interface for running Unreal Engine operations such as building plugins and running the editor.
/// </summary>
public interface IUnrealEngineRunner
{
    /// <summary>
    /// Builds a plugin using Unreal Engine tools.
    /// </summary>
    /// <param name="pluginPath">Path to the plugin .uplugin file.</param>
    /// <param name="outputPath">Path to output the built plugin.</param>
    /// <param name="engineVersion">Unreal Engine version number.</param>
    /// <param name="targetPlatforms">Target platforms to build for (comma separated).</param>
    /// <param name="logFilePath">Optional path to save the build log file.</param>
    /// <returns>Async task.</returns>
    Task BuildPlugin(string pluginPath, string outputPath, int engineVersion, string targetPlatforms,
        string logFilePath = "");

    /// <summary>
    /// Runs Unreal Editor with specified command line arguments.
    /// </summary>
    /// <param name="editorPath">Path to UnrealEditor executable.</param>
    /// <param name="arguments">Command line arguments.</param>
    /// <param name="workingDirectory">Working directory.</param>
    /// <param name="logFilePath">Optional path to save the editor log file.</param>
    /// <returns>Async task.</returns>
    Task RunUnrealEditor(string editorPath, string arguments, string workingDirectory, string logFilePath = "");

    /// <summary>
    /// Runs a build script with specified command line arguments.
    /// </summary>
    /// <param name="buildScriptPath">Path to build script (Build.bat, Build.sh).</param>
    /// <param name="arguments">Command line arguments.</param>
    /// <param name="workingDirectory">Working directory.</param>
    /// <param name="logFilePath">Optional path to save the script log file.</param>
    /// <returns>Async task.</returns>
    Task RunBuildScript(string buildScriptPath, string arguments, string workingDirectory, string logFilePath = "");
}