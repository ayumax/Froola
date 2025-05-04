using System.Text;
using Froola.Commands.Plugin;
using Froola.Commands.Plugin.Builder;
using Froola.Interfaces;
using Moq;

namespace Froola.Tests.Commands.Plugin.Builder;

public class TestResultsEvaluatorTests
{
    private readonly Mock<IFroolaLogger<TestResultsEvaluator>> _mockLogger = new();
    private readonly Mock<IFileSystem> _mockFileSystem = new();
    private readonly EditorPlatform _platform = EditorPlatform.Windows;
    private readonly UEVersion _engineVersion = UEVersion.UE_5_3;

    private TestResultsEvaluator CreateEvaluator()
    {
        return new TestResultsEvaluator(_mockLogger.Object, _mockFileSystem.Object);
    }

    private static MemoryStream CreateStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    [Fact]
    public void EvaluateTestResults_FileNotFound_ReturnsFailed()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        var evaluator = CreateEvaluator();
        var result = evaluator.EvaluateTestResults("dummy.json", _platform, _engineVersion);
        Assert.Equal(BuildStatus.Failed, result);
    }

    [Fact]
    public void EvaluateTestResults_ValidJson_Success()
    {
        const string json =
            "{\"succeeded\":1,\"succeededWithWarnings\":0,\"failed\":0,\"notRun\":0,\"totalDuration\":10}";
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.OpenRead(It.IsAny<string>())).Returns(CreateStream(json));
        var evaluator = CreateEvaluator();
        var result = evaluator.EvaluateTestResults("dummy.json", _platform, _engineVersion);
        Assert.Equal(BuildStatus.Success, result);
    }

    [Fact]
    public void EvaluateTestResults_ValidJson_Failed()
    {
        const string json =
            "{\"succeeded\":0,\"succeededWithWarnings\":0,\"failed\":1,\"notRun\":0,\"totalDuration\":10}";
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.OpenRead(It.IsAny<string>())).Returns(CreateStream(json));
        var evaluator = CreateEvaluator();
        var result = evaluator.EvaluateTestResults("dummy.json", _platform, _engineVersion);
        Assert.Equal(BuildStatus.Failed, result);
    }

    [Fact]
    public void EvaluateTestResults_InvalidJson_ReturnsFailed()
    {
        const string json = "invalid json";
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.OpenRead(It.IsAny<string>())).Returns(CreateStream(json));
        var evaluator = CreateEvaluator();
        var result = evaluator.EvaluateTestResults("dummy.json", _platform, _engineVersion);
        Assert.Equal(BuildStatus.Failed, result);
    }

    [Fact]
    public void EvaluateTestResults_Stream_ValidJson_Success()
    {
        const string json =
            "{\"succeeded\":1,\"succeededWithWarnings\":0,\"failed\":0,\"notRun\":0,\"totalDuration\":10}";
        var evaluator = CreateEvaluator();
        using var stream = CreateStream(json);
        var result = evaluator.EvaluateTestResults(stream, _platform, _engineVersion);
        Assert.Equal(BuildStatus.Success, result);
    }

    [Fact]
    public void EvaluateTestResults_Stream_ValidJson_Failed()
    {
        const string json =
            "{\"succeeded\":0,\"succeededWithWarnings\":0,\"failed\":1,\"notRun\":0,\"totalDuration\":10}";
        var evaluator = CreateEvaluator();
        using var stream = CreateStream(json);
        var result = evaluator.EvaluateTestResults(stream, _platform, _engineVersion);
        Assert.Equal(BuildStatus.Failed, result);
    }

    [Fact]
    public void EvaluateTestResults_Stream_InvalidJson_ReturnsFailed()
    {
        const string json = "invalid json";
        var evaluator = CreateEvaluator();
        using var stream = CreateStream(json);
        var result = evaluator.EvaluateTestResults(stream, _platform, _engineVersion);
        Assert.Equal(BuildStatus.Failed, result);
    }

    [Fact]
    public void EvaluatePackageBuildResults_FileNotFound_ReturnsFailed()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        var evaluator = CreateEvaluator();
        var result = evaluator.EvaluatePackageBuildResults("dummy.uplugin", _platform, _engineVersion);
        Assert.Equal(BuildStatus.Failed, result);
    }

    [Fact]
    public void EvaluatePackageBuildResults_ValidJson_EngineVersionMatch_Success()
    {
        const string json = "{\"EngineVersion\": \"5.3.0\"}";
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.OpenRead(It.IsAny<string>())).Returns(CreateStream(json));
        var evaluator = CreateEvaluator();
        var result = evaluator.EvaluatePackageBuildResults("dummy.uplugin", _platform, _engineVersion);
        Assert.Equal(BuildStatus.Success, result);
    }

    [Fact]
    public void EvaluatePackageBuildResults_ValidJson_EngineVersionMismatch_ReturnsFailed()
    {
        const string json = "{\"EngineVersion\": \"4.27.0\"}";
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.OpenRead(It.IsAny<string>())).Returns(CreateStream(json));
        var evaluator = CreateEvaluator();
        var result = evaluator.EvaluatePackageBuildResults("dummy.uplugin", _platform, _engineVersion);
        Assert.Equal(BuildStatus.Failed, result);
    }

    [Fact]
    public void EvaluatePackageBuildResults_InvalidJson_ReturnsFailed()
    {
        const string json = "invalid json";
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.OpenRead(It.IsAny<string>())).Returns(CreateStream(json));
        var evaluator = CreateEvaluator();
        var result = evaluator.EvaluatePackageBuildResults("dummy.uplugin", _platform, _engineVersion);
        Assert.Equal(BuildStatus.Failed, result);
    }

    [Fact]
    public void EvaluatePackageBuildResults_MissingEngineVersion_ReturnsFailed()
    {
        const string json = "{\"OtherProperty\": \"value\"}";
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.OpenRead(It.IsAny<string>())).Returns(CreateStream(json));
        var evaluator = CreateEvaluator();
        var result = evaluator.EvaluatePackageBuildResults("dummy.uplugin", _platform, _engineVersion);
        Assert.Equal(BuildStatus.Failed, result);
    }

    [Fact]
    public void EvaluatePackageBuildResults_Stream_EngineVersionMatch_Success()
    {
        const string json = "{\"EngineVersion\": \"5.3.0\"}";
        var evaluator = CreateEvaluator();
        using var stream = CreateStream(json);
        var result = evaluator.EvaluatePackageBuildResults(stream, _platform, _engineVersion);
        Assert.Equal(BuildStatus.Success, result);
    }

    [Fact]
    public void EvaluatePackageBuildResults_Stream_EngineVersionMismatch_ReturnsFailed()
    {
        const string json = "{\"EngineVersion\": \"4.27.0\"}";
        var evaluator = CreateEvaluator();
        using var stream = CreateStream(json);
        var result = evaluator.EvaluatePackageBuildResults(stream, _platform, _engineVersion);
        Assert.Equal(BuildStatus.Failed, result);
    }

    [Fact]
    public void EvaluatePackageBuildResults_Stream_InvalidJson_ReturnsFailed()
    {
        const string json = "invalid json";
        var evaluator = CreateEvaluator();
        using var stream = CreateStream(json);
        var result = evaluator.EvaluatePackageBuildResults(stream, _platform, _engineVersion);
        Assert.Equal(BuildStatus.Failed, result);
    }

    [Fact]
    public void EvaluatePackageBuildResults_Stream_MissingEngineVersion_ReturnsFailed()
    {
        const string json = "{\"OtherProperty\": \"value\"}";
        var evaluator = CreateEvaluator();
        using var stream = CreateStream(json);
        var result = evaluator.EvaluatePackageBuildResults(stream, _platform, _engineVersion);
        Assert.Equal(BuildStatus.Failed, result);
    }
}