using System.Text;
using Froola.Interfaces;
using Froola.Runners;
using Froola.Configs.Attributes;
using Moq;

namespace Froola.Tests.Runners;

public class ConfigJsonExporterTests
{
    [Fact]
    public async Task ExportConfigJson_WritesSerializedConfigToFile()
    {
        // Arrange
        var mockFileSystem = new Mock<IFileSystem>();
        var memoryStream = new NonDisposableMemoryStream();
        string? writtenJson = null;
        const string testPath = "C:/tmp/test.json";
        var testConfig = new DummyConfig { Value = "test" };
        var configs = new object[] { testConfig };

        mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(fs => fs.Create(It.IsAny<string>())).Returns(memoryStream);

        var exporter = new ConfigJsonExporter(mockFileSystem.Object);

        // Act
        await exporter.ExportConfigJson(testPath, configs);

        // Assert
        memoryStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(memoryStream, Encoding.UTF8, leaveOpen: true);
        writtenJson = await reader.ReadToEndAsync();
        Assert.False(string.IsNullOrWhiteSpace(writtenJson));
        Assert.Contains("DummyConfig", writtenJson);
        Assert.Contains("test", writtenJson);
    }

    [OptionClass("DummyConfig")]
    private class DummyConfig
    {
        public string Value { get; set; } = string.Empty;
    }

    // This custom MemoryStream disables Dispose to prevent the stream from being closed
    // when used with 'await using' in the production code. This allows the test to access
    // the stream after ExportConfigJson has finished, which would otherwise throw an
    // ObjectDisposedException. This is an irregular workaround specifically for testing.
    private class NonDisposableMemoryStream : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            // Do nothing
        }
    }
}