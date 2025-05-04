using System;
using System.ComponentModel;
using System.Globalization;

namespace Froola.Configs.Converters;

public class UEVersionTypeConverter : TypeConverter
{
    // Converts UEVersion enum to string (逆のロジック: UEVersionExtensions.ToVersionString)
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value,
        Type destinationType)
    {
        if (destinationType == typeof(string) && value is UEVersion ueVersion)
        {
            return ueVersion.ToVersionString();
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    // 文字列→UEVersion（Parseの逆方向は不要、TypeConverterのConvertFromはParse相当になる）
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string str)
        {
            return UEVersionExtensions.Parse(str);
        }

        return base.ConvertFrom(context, culture, value);
    }
}