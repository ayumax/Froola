using System;
using System.Runtime.CompilerServices;
using Froola.Interfaces;

namespace Froola.Utils;

/// <summary>
///     Generic logger implementation for Froola. Handles log output to console and file.
/// </summary>
public class FroolaLogger<T>(IFroolaLogger logger) : IFroolaLogger<T>
{
    /// <summary>
    ///     Sets the directory where log files are saved and initializes the logger.
    /// </summary>
    public void SetSaveDirectory(string directory)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Logs an informational message.
    /// </summary>
    public void LogInformation(string message, [CallerMemberName] string callerName = "")
    {
        logger.LogInformation($"{typeof(T).Name}.{callerName}: {message}", string.Empty);
    }

    /// <summary>
    ///     Logs a warning message.
    /// </summary>
    public void LogWarning(string message, [CallerMemberName] string callerName = "")
    {
        logger.LogWarning($"{typeof(T).Name}.{callerName}: {message}", string.Empty);
    }

    /// <summary>
    ///     Logs an error message.
    /// </summary>
    public void LogError(string message, [CallerMemberName] string callerName = "")
    {
        logger.LogError($"{typeof(T).Name}.{callerName}: {message}", string.Empty);
    }

    /// <summary>
    ///     Logs an error message with exception details.
    /// </summary>
    public void LogError(string message, Exception e, [CallerMemberName] string callerName = "")
    {
        logger.LogError($"{typeof(T).Name}.{callerName}: {message}", e, string.Empty);
    }

    /// <summary>
    ///     Logs a debug message.
    /// </summary>
    public void LogDebug(string message, [CallerMemberName] string callerName = "")
    {
        logger.LogDebug($"{typeof(T).Name}.{callerName}: {message}", string.Empty);
    }
}