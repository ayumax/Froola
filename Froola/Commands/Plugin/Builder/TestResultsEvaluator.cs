using System;
using System.IO;
using System.Text.Json;
using Froola.Interfaces;

namespace Froola.Commands.Plugin.Builder;

/// <summary>
/// Evaluates test and package build results from test reports and file existence checks.
/// </summary>
public class TestResultsEvaluator(IFroolaLogger<TestResultsEvaluator> logger, IFileSystem fileSystem)
    : ITestResultsEvaluator
{
    /// <summary>
    /// JSON serializer options for formatting output.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    
    /// <summary>
    /// Evaluates test results from a JSON stream.
    /// </summary>
    /// <param name="indexJsonFile">Stream containing test report JSON.</param>
    /// <param name="platform">Editor platform.</param>
    /// <param name="engineVersion">Engine version string.</param>
    /// <returns>Build status based on test results.</returns>
    public BuildStatus EvaluateTestResults(Stream indexJsonFile, EditorPlatform platform, UEVersion engineVersion)
    {
        try
        {
            using var reader = new StreamReader(indexJsonFile);
            var jsonContent = reader.ReadToEnd();
            var testReport = JsonSerializer.Deserialize<TestReport>(jsonContent, SerializerOptions);

            if (testReport == null)
            {
                logger.LogInformation(
                    $"[{platform} {engineVersion.ToFullVersionString()}] ERROR: Failed to parse index.json - tests failed");
                return BuildStatus.Failed;
            }

            // Log summary of test results
            logger.LogInformation($"[{platform} {engineVersion.ToFullVersionString()}] Test report summary:");
            logger.LogInformation(
                $"[{platform} {engineVersion.ToFullVersionString()}] - Tests succeeded: {testReport.succeeded}");
            logger.LogInformation(
                $"[{platform} {engineVersion.ToFullVersionString()}] - Tests with warnings: {testReport.succeededWithWarnings}");
            logger.LogInformation(
                $"[{platform} {engineVersion.ToFullVersionString()}] - Tests failed: {testReport.failed}");
            logger.LogInformation(
                $"[{platform} {engineVersion.ToFullVersionString()}] - Tests not run: {testReport.notRun}");
            logger.LogInformation(
                $"[{platform} {engineVersion.ToFullVersionString()}] - Total duration: {testReport.totalDuration} seconds");

            var success = testReport.failed == 0;
            logger.LogInformation(
                $"[{platform} {engineVersion.ToFullVersionString()}] Test result: {(success ? "PASSED" : "FAILED")}");

            return success ? BuildStatus.Success : BuildStatus.Failed;
        }
        catch (Exception ex)
        {
            logger.LogError(
                $"[{platform} {engineVersion.ToFullVersionString()}] ERROR evaluating test results: {ex.Message}");
            return BuildStatus.Failed;
        }
    }

    /// <summary>
    /// Evaluates test results from a JSON file path.
    /// </summary>
    /// <param name="indexJsonPath">Path to test report JSON file.</param>
    /// <param name="osType">Editor platform.</param>
    /// <param name="engineVersion">Engine version string.</param>
    /// <returns>Build status based on test results.</returns>
    public BuildStatus EvaluateTestResults(string indexJsonPath, EditorPlatform osType, UEVersion engineVersion)
    {
        try
        {
            logger.LogInformation(
                $"[{osType} {engineVersion.ToFullVersionString()}] Checking for index.json at {indexJsonPath}");

            if (!fileSystem.FileExists(indexJsonPath))
            {
                logger.LogInformation(
                    $"[{osType} {engineVersion.ToFullVersionString()}] ERROR: index.json not found - tests failed");
                return BuildStatus.Failed;
            }

            using var stream = fileSystem.OpenRead(indexJsonPath);
            return EvaluateTestResults(stream, osType, engineVersion);
        }
        catch (Exception ex)
        {
            logger.LogError(
                $"[{osType} {engineVersion.ToFullVersionString()}] ERROR evaluating test results: {ex.Message}");
            return BuildStatus.Failed;
        }
    }
    
    /// <summary>
    /// Evaluates package build results by checking for the existence of the .uplugin file.
    /// </summary>
    /// <param name="upluginPath">Path to the .uplugin file.</param>
    /// <param name="platform">Editor platform.</param>
    /// <param name="engineVersion">Engine version string.</param>
    /// <returns>Build status based on package existence.</returns>
    public BuildStatus EvaluatePackageBuildResults(string upluginPath, EditorPlatform platform, UEVersion engineVersion)
    {
        // Check if upluginPath exists
        if (!fileSystem.FileExists(upluginPath))
        {
            logger.LogInformation(
                $"[{platform} {engineVersion.ToFullVersionString()}] ERROR: uplugin file not found: {upluginPath}");
            return BuildStatus.Failed;
        }

        // Open file as stream and delegate to stream version
        using var stream = fileSystem.OpenRead(upluginPath);
        return EvaluatePackageBuildResults(stream, platform, engineVersion);
    }

    /// <summary>
    /// Evaluates package build results by checking the contents of the .uplugin file.
    /// </summary>
    /// <param name="upluginStream">Stream containing the .uplugin file contents.</param>
    /// <param name="platform">Editor platform.</param>
    /// <param name="engineVersion">Engine version string.</param>
    /// <returns>Build status based on package contents.</returns>
    public BuildStatus EvaluatePackageBuildResults(Stream upluginStream, EditorPlatform platform,
        UEVersion engineVersion)
    {
        try
        {
            using var reader = new StreamReader(upluginStream);
            var jsonContent = reader.ReadToEnd();
            var jsonDoc = JsonDocument.Parse(jsonContent);
            if (!jsonDoc.RootElement.TryGetProperty("EngineVersion", out var engineVersionElement))
            {
                logger.LogInformation(
                    $"[{platform} {engineVersion.ToFullVersionString()}] ERROR: EngineVersion property not found in uplugin stream");
                return BuildStatus.Failed;
            }

            var engineVersionInUplugin = engineVersionElement.GetString();
            if (engineVersionInUplugin != $"{engineVersion.ToVersionString()}.0")
            {
                logger.LogInformation(
                    $"[{platform} {engineVersion.ToFullVersionString()}] ERROR: EngineVersion mismatch in stream. uplugin: {engineVersionInUplugin}, expected: {engineVersion}");
                return BuildStatus.Failed;
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation(
                $"[{platform} {engineVersion.ToFullVersionString()}] ERROR: Failed to parse uplugin stream as JSON: {ex.Message}");
            return BuildStatus.Failed;
        }

        return BuildStatus.Success;
    }
}