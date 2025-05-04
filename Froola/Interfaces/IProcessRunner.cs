using System.Collections.Generic;

namespace Froola.Interfaces;

/// <summary>
/// Interface for running external processes and streaming their output.
/// </summary>
public interface IProcessRunner
{
    IAsyncEnumerable<string> RunAsync(string fileName, string arguments, string workingDirectory, IDictionary<string, string>? environmentVariables = null);
}
