using Froola.Annotations;

namespace Froola.Tests.Annotations;

public class EnumArrayAttributeTests
{
    [Fact]
    public void IsValid_Returns_Success_For_EnumValues()
    {
        // Arrange
        var attribute = new EnumArrayAttribute(typeof(DayOfWeek));
        var values = Enum.GetNames(typeof(DayOfWeek));

        // Act
        var result = attribute.IsValid(values);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid_Returns_Failure_For_InvalidEnumValue()
    {
        // Arrange
        var attribute = new EnumArrayAttribute(typeof(DayOfWeek));
        var values = new[] { "Monday", "Funday" }; // 'Funday' is not a valid DayOfWeek

        // Act
        var result = attribute.IsValid(values);

        // Assert
        Assert.False(result); // Should be false because 'Funday' is invalid
    }

    [Fact]
    public void IsValid_Returns_Success_For_NullValue()
    {
        // Arrange
        var attribute = new EnumArrayAttribute(typeof(DayOfWeek));

        // Act
        var result = attribute.IsValid(null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid_Returns_Success_For_NonArrayValue()
    {
        // Arrange
        var attribute = new EnumArrayAttribute(typeof(DayOfWeek));

        // Act
        var result = attribute.IsValid("Monday"); // Not an array

        // Assert
        Assert.True(result);
    }
}