using System;
using Froola.Interfaces;
using Xunit.Abstractions;

namespace Froola.Tests
{
    // Logger implementation for test. Outputs to xUnit console.
    public class TestFroolaLogger : IFroolaLogger
    {
        private readonly ITestOutputHelper _output;

        public TestFroolaLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public void SetSaveDirectory(string directory)
        {
            // No operation needed for test logger.
        }

        public void LogInformation(string message, string callerName = "")
        {
            var log = $"[INFO] [{callerName}] {message}";
            _output.WriteLine(log);
        }

        public void LogWarning(string message, string callerName = "")
        {
            var log = $"[WARN] [{callerName}] {message}";
            _output.WriteLine(log);
        }

        public void LogError(string message, string callerName = "")
        {
            var log = $"[ERROR] [{callerName}] {message}";
            _output.WriteLine(log);
        }

        public void LogError(string message, Exception e, string callerName = "")
        {
            var log = $"[ERROR] [{callerName}] {message} Exception: {e}";
            _output.WriteLine(log);
        }

        public void LogDebug(string message, string callerName = "")
        {
            var log = $"[DEBUG] [{callerName}] {message}";
            _output.WriteLine(log);
        }
    }
}
