using System;

namespace Froola;

/// <summary>
/// Supported operating systems for packaging.
/// </summary>
public enum GamePlatform
{
    /// <summary>
    ///     Windows 64-bit platform.
    /// </summary>
    Win64,

    /// <summary>
    ///     Mac platform.
    /// </summary>
    Mac,

    /// <summary>
    ///     Linux platform.
    /// </summary>
    Linux,

    /// <summary>
    ///     iOS platform.
    /// </summary>
    IOS,

    /// <summary>
    ///     Android platform.
    /// </summary>
    Android
}

/// <summary>
///     Extension methods for GamePlatform enum.
/// </summary>
public static class GamePlatformExtensions
{
    /// <summary>
    ///     Parses a string value to a GamePlatform enum.
    /// </summary>
    /// <param name="value">String representation of the game platform.</param>
    /// <returns>Parsed GamePlatform value.</returns>
    /// <exception cref="ArgumentException">Thrown if the value is not a valid GamePlatform.</exception>
    public static GamePlatform Parse(string value)
    {
        if (Enum.TryParse<GamePlatform>(value, ignoreCase: true, out var mode))
        {
            return mode;
        }

        throw new ArgumentException($"Invalid build mode: {value}");
    }
}