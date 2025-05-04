using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using Froola.Configs.Converters;

namespace Froola;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[TypeConverter(typeof(UEVersionTypeConverter))]
public enum UEVersion
{
    UE_5_0,
    UE_5_1,
    UE_5_2,
    UE_5_3,
    UE_5_4,
    UE_5_5,
    UE_5_6,
    UE_5_7,
    UE_5_8,
    UE_5_9,
    UE_5_10
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static partial class UEVersionExtensions
{
    public static UEVersion Parse(string value)
    {
        // Check patterns: 5.x, UE5.x, UE_5.x
        var input = value.Trim();
        if (string.IsNullOrEmpty(input))
        {
            throw new ArgumentException($"Invalid UE version: {value}");
        }

        // Regex patterns
        var regexList = new[]
        {
            UEVersionRegex1(), // 5.x
            UEVersionRegex2(), // UE5.x
            UEVersionRegex3(), // UE_5.x
            UEVersionRegex4() // UE_5_x
        };

        var enumName =
            (from regex in regexList
                select regex.Match(input)
                into match
                where match.Success
                select $"UE_5_{match.Groups[1].Value}").FirstOrDefault();

        if (enumName != null && Enum.TryParse<UEVersion>(enumName, true, out var version))
        {
            return version;
        }

        if (Enum.TryParse<UEVersion>(input, true, out var fallback))
        {
            return fallback;
        }

        throw new ArgumentException($"Invalid UE version: {value}");
    }
    
    public static string ToVersionString(this UEVersion version)
    {
        return version.ToString().Replace("UE_", "").Replace("_", ".");
    }

    public static string ToFullVersionString(this UEVersion version)
    {
        return $"UE{version.ToVersionString()}";
    }

    [GeneratedRegex(@"^5\.(\d+)$", RegexOptions.IgnoreCase, "ja-JP")]
    private static partial Regex UEVersionRegex1();

    [GeneratedRegex(@"^UE5\.(\d+)$", RegexOptions.IgnoreCase, "ja-JP")]
    private static partial Regex UEVersionRegex2();

    [GeneratedRegex(@"^UE_5\.(\d+)$", RegexOptions.IgnoreCase, "ja-JP")]
    private static partial Regex UEVersionRegex3();

    [GeneratedRegex(@"^UE_5_(\d+)$", RegexOptions.IgnoreCase, "ja-JP")]
    private static partial Regex UEVersionRegex4();
}