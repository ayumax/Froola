using System.Collections.Generic;
using Cysharp.Diagnostics;
using Froola.Interfaces;

namespace Froola.Runners;

/// <summary>
/// Implementation of IProcessRunner using ProcessX.
/// </summary>
public class ProcessRunner : IProcessRunner
{
    public IAsyncEnumerable<string> RunAsync(string fileName, string arguments, string workingDirectory, IDictionary<string, string>? environmentVariables = null)
    {
        if (environmentVariables is null)
        {
            return ProcessX.StartAsync(fileName, arguments, workingDirectory);
        }

        return ProcessX.StartAsync(fileName, arguments, workingDirectory, environmentVariables);
    }
}
