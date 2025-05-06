using System;
using System.ComponentModel.DataAnnotations;

namespace Froola.Annotations;

public class UeVersionEnumArrayAttribute : ValidationAttribute
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
                _ = UEVersionExtensions.Parse(item);
            }
        }
        catch (Exception e)
        {
            return new ValidationResult(e.Message);
        }


        return ValidationResult.Success;
    }
}