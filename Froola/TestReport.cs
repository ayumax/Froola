using System.Text.Json.Serialization;

namespace Froola;

/// <summary>
/// Structure to hold test report data from index.json
/// </summary>
public class TestReport
{
    /// <summary>
    /// Number of succeeded tests
    /// </summary>
    [JsonPropertyName("succeeded")]
    public int succeeded { get; set; }

    /// <summary>
    /// Number of succeeded tests with warnings
    /// </summary>
    [JsonPropertyName("succeededWithWarnings")]
    public int succeededWithWarnings { get; set; }

    /// <summary>
    /// Number of failed tests
    /// </summary>
    [JsonPropertyName("failed")]
    public int failed { get; set; }

    /// <summary>
    /// Number of tests not run
    /// </summary>
    [JsonPropertyName("notRun")]
    public int notRun { get; set; }

    /// <summary>
    /// Number of tests still in process
    /// </summary>
    [JsonPropertyName("inProcess")]
    public int inProcess { get; set; }

    /// <summary>
    /// Total duration of all tests
    /// </summary>
    [JsonPropertyName("totalDuration")]
    public double totalDuration { get; set; }

    // Other fields from index.json are omitted as we don't need them for evaluation
}