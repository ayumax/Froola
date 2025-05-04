using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Froola.Configs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Froola.Utils
{
    public static class ServiceCollectionHelper
    {
        private static readonly Dictionary<Type, MethodInfo> ConfigureMethods = new();
        
        /// <summary>
        /// Extension method to call IServiceCollection.Configure(IConfigurationSection) with type argument.
        /// </summary>
        public static IServiceCollection ConfigureConfig(this IServiceCollection services, Type configType,
            IConfiguration configuration)
        {
            if (!ConfigureMethods.TryGetValue(configType, out var genericMethod))
            {
                var configureMethod = typeof(OptionsConfigurationServiceCollectionExtensions)
                    .GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .First(m => m is { Name: "Configure", IsGenericMethod: true }
                                && m.GetParameters().Length == 2
                                && m.GetParameters()[1].ParameterType == typeof(IConfiguration));

                genericMethod = configureMethod.MakeGenericMethod(configType);
                ConfigureMethods[configType] = genericMethod;
            }

            var sectionName = ConfigHelper.GetSection(configType);
            var section = configuration.GetSection(sectionName);

            genericMethod.Invoke(null, [services, section]);
            return services;
        }

        /// <summary>
        ///     If a class with the name "ConfigName+PostConfigure" exists in the assembly and implements
        ///     IPostConfigureOptions(T)
        /// </summary>
        /// <param name="services">IServiceCollection</param>
        /// <param name="configType">Config class type</param>
        /// <returns>IServiceCollection</returns>
        public static IServiceCollection AddPostConfigureIfExists(this IServiceCollection services, Type configType)
        {
            var postConfigureTypeName = configType.Name + "PostConfigure";
            var assembly = configType.Assembly;
            var postConfigureType = assembly.GetTypes().FirstOrDefault(t => t.Name == postConfigureTypeName);
            if (postConfigureType == null)
            {
                return services;
            }

            var ipostConfigureType = typeof(IPostConfigureOptions<>).MakeGenericType(configType);
            if (!ipostConfigureType.IsAssignableFrom(postConfigureType))
            {
                return services;
            }

            services.AddSingleton(ipostConfigureType, postConfigureType);
            return services;
        }
    }
}
