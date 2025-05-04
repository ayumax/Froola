using System.IO;
using Froola.Commands.Plugin;
using Froola;

namespace Froola.Interfaces;

/// <summary>
/// Interface for evaluating test and package build results.
/// </summary>
public interface ITestResultsEvaluator
{
    /// <summary>
    /// Evaluates test results from a JSON file path.
    /// </summary>
    /// <param name="indexJsonPath">Path to test report JSON file.</param>
    /// <param name="platform">Editor platform.</param>
    /// <param name="engineVersion">Engine version</param>
    /// <returns>Build status based on test results.</returns>
    BuildStatus EvaluateTestResults(string indexJsonPath, EditorPlatform platform, UEVersion engineVersion);

    /// <summary>
    /// Evaluates test results from a JSON stream.
    /// </summary>
    /// <param name="indexJsonFile">Stream containing test report JSON.</param>
    /// <param name="platform">Editor platform.</param>
    /// <param name="engineVersion">Engine version</param>
    /// <returns>Build status based on test results.</returns>
    BuildStatus EvaluateTestResults(Stream indexJsonFile, EditorPlatform platform, UEVersion engineVersion);

    /// <summary>
    /// Evaluates package build results by checking for the existence of the .uplugin file.
    /// </summary>
    /// <param name="upluginPath">Path to the .uplugin file.</param>
    /// <param name="platform">Editor platform.</param>
    /// <param name="engineVersion">Engine version</param>
    /// <returns>Build status based on package existence.</returns>
    BuildStatus EvaluatePackageBuildResults(string upluginPath, EditorPlatform platform, UEVersion engineVersion);

    /// <summary>
    /// Evaluates package build results by checking the contents of the .uplugin file.
    /// </summary>
    /// <param name="upluginStream">Stream containing the .uplugin file contents.</param>
    /// <param name="platform">Editor platform.</param>
    /// <param name="engineVersion">Engine version</param>
    /// <returns>Build status based on package contents.</returns>
    BuildStatus EvaluatePackageBuildResults(Stream upluginStream, EditorPlatform platform, UEVersion engineVersion);
}