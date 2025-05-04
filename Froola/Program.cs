using ConsoleAppFramework;
using Froola;
using Froola.Configs;

var dependencyResolver = new DependencyResolver();
using var host = dependencyResolver.BuildHost(ConfigHelper.GetAllConfigTypes());

dependencyResolver.OutputAllConfigValues();

var app = ConsoleApp.Create();
ConsoleApp.ServiceProvider = host.Services;

app.Run(args);