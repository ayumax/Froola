using System;

namespace Froola;

/// <summary>
/// Supported operating systems for testing.
/// </summary>
public enum EditorPlatform
{
    /// <summary>
    /// Windows platform.
    /// </summary>
    Windows,
    /// <summary>
    /// Mac platform.
    /// </summary>
    Mac,
    /// <summary>
    /// Linux platform.
    /// </summary>
    Linux
}

/// <summary>
/// Extension methods for EditorPlatform enum.
/// </summary>
public static class EditorPlatformExtensions
{
    /// <summary>
    /// Parses a string value to an EditorPlatform enum.
    /// </summary>
    /// <param name="value">String representation of the platform.</param>
    /// <returns>Parsed EditorPlatform value.</returns>
    /// <exception cref="ArgumentException">Thrown if the value is not a valid EditorPlatform.</exception>
    public static EditorPlatform Parse(string value)
    {
        if (Enum.TryParse<EditorPlatform>(value, ignoreCase: true, out var mode))
        {
            return mode;
        }

        throw new ArgumentException($"Invalid build mode: {value}");
    }
}