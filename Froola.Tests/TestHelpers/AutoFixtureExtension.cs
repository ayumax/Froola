using AutoFixture;
using AutoFixture.Kernel;

namespace Froola.Tests.TestHelpers;

public static class AutoFixtureExtension
{
    public static object CreateWithType(this IFixture fixture, Type type)
    {
        var context = new SpecimenContext(fixture);
        return context.Resolve(type);
    }
}