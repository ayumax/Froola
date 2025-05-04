using System;

namespace Froola.Configs.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class OptionClassAttribute(string section) : Attribute
{
    public string Section { get; private set; } = section;
}