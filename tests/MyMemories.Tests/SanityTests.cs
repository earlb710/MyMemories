namespace MyMemories.Tests;

/// <summary>
/// Simple sanity test to verify test infrastructure works.
/// Run these tests first to verify everything is set up correctly.
/// </summary>
public class SanityTests
{
    [Fact]
    public void BasicTest_ShouldPass()
    {
        // Arrange
        var expected = 2;

        // Act
        var actual = 1 + 1;

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void StringTest_ShouldPass()
    {
        // Arrange
        var text = "Hello";

        // Act
        var result = text + " World";

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void BoolTest_ShouldPass()
    {
        // Arrange & Act
        var result = true;

        // Assert
        Assert.True(result);
    }
}

