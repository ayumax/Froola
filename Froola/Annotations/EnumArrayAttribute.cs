using System;
using System.ComponentModel.DataAnnotations;

namespace Froola.Annotations;

public class EnumArrayAttribute(Type enumType) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var array = value as string[];
        if (array == null)
        {
            return ValidationResult.Success;
        }

        foreach (var item in array)
        {
            if (!Enum.IsDefined(enumType, item))
            {
                return new ValidationResult($"'{item}' is not a valid value for {enumType.Name}");
            }
        }

        return ValidationResult.Success;
    }
}