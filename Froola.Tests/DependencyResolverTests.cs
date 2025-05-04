using Froola.Configs;
using Froola.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Froola.Tests;

public class DependencyResolverTests(ITestOutputHelper outputHelper) : IDisposable
{
    private readonly DependencyResolver _resolver = new();

    public void Dispose()
    {
        _resolver.Dispose();
    }

    [Fact]
    public void Resolve_ThrowsException_WhenNotInitialized()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.Resolve<object>());
        Assert.Contains("not initialized", ex.Message);
    }

    [Fact]
    public void Resolve_ByType_ThrowsException_WhenNotInitialized()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.Resolve(typeof(object)));
        Assert.Contains("not initialized", ex.Message);
    }

    [Fact]
    public void BuildHostWithContainerBuilder_RegistersAndResolvesService()
    {
        // Arrange
        const string logPath = "./dummy";
        var additional = new object[] { new DummyService() };
        var host = _resolver.BuildHostWithContainerBuilder(new DummyContainerBuilder(outputHelper), logPath,
            additional);

        // Act
        var resolved = _resolver.Resolve<DummyService>();

        // Assert
        Assert.NotNull(resolved);
        Assert.IsType<DummyService>(resolved);
    }

    [Fact]
    public void BuildHostWithContainerBuilder_Generic_RegistersAndResolvesService()
    {
        const string logPath = "./dummy";
        var additional = new object[] { new DummyService() };
        var host = _resolver.BuildHostWithContainerBuilder(new DummyContainerBuilder(outputHelper), logPath,
            additional);
        var resolved = _resolver.Resolve<DummyService>();
        Assert.NotNull(resolved);
    }

    [Fact]
    public void BuildHostWithContainerBuilder_ThrowsException_IfNotContainerBuilder()
    {
        const string logPath = "./dummy";
        var additional = Array.Empty<object>();
        var ex = Assert.Throws<ArgumentException>(() =>
            _resolver.BuildHostWithContainerBuilder(typeof(object), logPath, additional));
        Assert.Contains("does not implement", ex.Message);
    }

    [Fact]
    public void Dispose_AllowsRebuild()
    {
        const string logPath = "./dummy";
        var additional = new object[] { new DummyService() };
        _resolver.BuildHostWithContainerBuilder(new DummyContainerBuilder(outputHelper), logPath, additional);
        _resolver.Dispose();
        var host = _resolver.BuildHostWithContainerBuilder(new DummyContainerBuilder(outputHelper), logPath,
            additional);
        var resolved = _resolver.Resolve<DummyService>();
        Assert.NotNull(resolved);
    }

    [Fact]
    public void OutputAllConfigValues_LogsConfigValues()
    {
        _resolver.BuildHost(ConfigHelper.GetAllConfigTypes());

        _resolver.OutputAllConfigValues();
    }

    private class DummyService
    {
    }

    private class DummyContainerBuilder(ITestOutputHelper outputHelper) : IContainerBuilder
    {
        public void Register(IServiceCollection services)
        {
            services.AddSingleton(outputHelper);
            services.AddSingleton<IFroolaLogger, TestFroolaLogger>();
        }
    }
}