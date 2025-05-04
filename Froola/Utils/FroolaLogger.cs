using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Froola.Interfaces;
using Microsoft.Extensions.Logging;
using ZLogger;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Froola.Utils
{
    /// <summary>
    /// Logger implementation for Froola. Handles log output to console and file.
    /// </summary>
    [SuppressMessage("Usage", "CA2254:Template should be a static expression")]
    public class FroolaLogger : IFroolaLogger
    {
        private ILogger? _logger;
        private ILoggerFactory? _factory;
        private readonly Queue<(LogLevel, string)> _logQueue = new();

        /// <summary>
        /// Sets the directory where log files are saved and initializes the logger.
        /// </summary>
        public void SetSaveDirectory(string directory)
        {
            if (_logger is not null)
            {
                return;
            }

            // Create log directory
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create log file path
            var logFilePath = Path.Combine(directory, "froola.log");

            // Create Microsoft ILogger from Serilog
            _factory = LoggerFactory.Create(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Trace);

                // Add ZLogger provider to ILoggingBuilder
                logging.AddZLoggerConsole();

                logging.AddZLoggerFile(logFilePath);
            });

            _logger = _factory.CreateLogger<FroolaLogger>();

            _logger.LogInformation($"Logger initialized. Log file: {logFilePath}");
            
            while (_logQueue.Count > 0)
            {
                var (level, message) = _logQueue.Dequeue();
                _logger?.Log(level, message);
            }
        }
        
        /// <summary>
        /// Logs an informational message.
        /// </summary>
        public void LogInformation(string message, string callerName)
        {
            if (_logger is null)
            {
                _logQueue.Enqueue((LogLevel.Information, message));
                return;
            }
            
            _logger?.LogInformation(message);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        public void LogWarning(string message, string callerName)
        {
            if (_logger is null)
            {
                _logQueue.Enqueue((LogLevel.Warning, message));
                return;
            }
            
            _logger?.LogWarning(message);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        public void LogError(string message, string callerName)
        {
            if (_logger is null)
            {
                _logQueue.Enqueue((LogLevel.Error, message));
                return;
            }
            
            _logger?.LogError(message);
        }
        
        /// <summary>
        /// Logs an error message with exception details.
        /// </summary>
        public void LogError(string message, Exception e, string callerName)
        {
            if (_logger is null)
            {
                _logQueue.Enqueue((LogLevel.Error, message));
                return;
            }
            
            _logger?.LogError(message, e);
        }

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        public void LogDebug(string message, string callerName)
        {
            if (_logger is null)
            {
                _logQueue.Enqueue((LogLevel.Debug, message));
                return;
            }
            
            _logger?.LogDebug(message);
        }
    } 
}