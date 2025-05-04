using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Froola.Configs.Attributes;
using Microsoft.Extensions.Options;

namespace Froola.Configs;

public static class ConfigHelper
{
    private static readonly Dictionary<Type, string> SectionCache = new();

    private static readonly Dictionary<Type, Type> OptionsTypeCache = new();
    private static readonly Dictionary<Type, PropertyInfo> OptionsValuePropertyCache = new();


    [SuppressMessage("ReSharper", "JoinNullCheckWithUsage")]
    public static string GetSection(Type configType)
    {
        if (SectionCache.TryGetValue(configType, out var section))
        {
            return section;
        }

        // Get OptionClassAttribute from configType
        if (Attribute.GetCustomAttribute(configType,
                typeof(OptionClassAttribute)) is not OptionClassAttribute attribute)
        {
            throw new InvalidOperationException($"OptionClassAttribute not found in config class : {configType.Name}");
        }

        section = attribute.Section;

        if (string.IsNullOrEmpty(section))
        {
            throw new InvalidOperationException(
                $"Section property in OptionClassAttribute is null or empty in config class : {configType.Name}");
        }

        SectionCache[configType] = section;

        return section;
    }

    public static string GetSection<TConfig>()
    {
        return GetSection(typeof(TConfig));
    }

    public static Type GetIOptionsType(Type configType)
    {
        if (OptionsTypeCache.TryGetValue(configType, out var optionsType))
        {
            return optionsType;
        }

        optionsType = typeof(IOptions<>).MakeGenericType(configType);
        OptionsTypeCache[configType] = optionsType;

        return optionsType;
    }

    public static PropertyInfo GetIOptionsValuePropertyInfo(Type configType)
    {
        if (OptionsValuePropertyCache.TryGetValue(configType, out var info))
        {
            return info;
        }

        info = GetIOptionsType(configType).GetProperty("Value");

        OptionsValuePropertyCache[configType] = info ??
                                                throw new InvalidOperationException(
                                                    $"Value property not found in {configType.Name} IOptions");

        return info;
    }

    public static Type[] GetAllConfigTypes()
    {
        return Assembly.GetAssembly(typeof(PluginConfig))!.GetTypes()
            .Where(x => x.GetCustomAttribute<OptionClassAttribute>() != null)
            .ToArray();
    }
}