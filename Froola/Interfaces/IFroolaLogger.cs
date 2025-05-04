using System;
using System.Runtime.CompilerServices;

namespace Froola.Interfaces
{
    /// <summary>
    /// Interface for logging messages in the Froola system.
    /// </summary>
    public interface IFroolaLogger
    {
        /// <summary>
        /// Sets the directory where logs are saved.
        /// </summary>
        /// <param name="directory">The directory to save logs in.</param>
        void SetSaveDirectory(string directory);

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="callerName">The name of the calling method (optional).</param>
        void LogInformation(string message, [CallerMemberName] string callerName = "");

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        /// <param name="callerName">The name of the calling method (optional).</param>
        void LogWarning(string message, [CallerMemberName] string callerName = "");

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        /// <param name="callerName">The name of the calling method (optional).</param>
        void LogError(string message, [CallerMemberName] string callerName = "");

        /// <summary>
        /// Logs an error message with an exception.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        /// <param name="e">The exception associated with the error.</param>
        /// <param name="callerName">The name of the calling method (optional).</param>
        void LogError(string message, Exception e, [CallerMemberName] string callerName = "");

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The debug message to log.</param>
        /// <param name="callerName">The name of the calling method (optional).</param>
        void LogDebug(string message, [CallerMemberName] string callerName = "");
    }

    /// <summary>
    /// Generic interface for logging messages in the PluginBuilder system.
    /// </summary>
    public interface IFroolaLogger<T> : IFroolaLogger
    {
    }
}
