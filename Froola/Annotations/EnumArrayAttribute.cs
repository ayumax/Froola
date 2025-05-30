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

        try
        {
            foreach (var item in array)
            {
                _ = Enum.Parse(enumType, item, true);
            }
        }
        catch (Exception e)
        {
            return new ValidationResult(e.Message);
        }
        

        return ValidationResult.Success;
    }
}