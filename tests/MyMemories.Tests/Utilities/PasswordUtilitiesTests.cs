using MyMemories.Utilities;

namespace MyMemories.Tests.Utilities;

/// <summary>
/// Unit tests for PasswordUtilities class.
/// Tests password hashing, verification, strength validation, and other password operations.
/// </summary>
public class PasswordUtilitiesTests
{
    [Fact]
    public void HashPassword_WithValidPassword_ReturnsNonEmptyHash()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = PasswordUtilities.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(password); // Hash should be different from original
    }

    [Fact]
    public void HashPassword_SamePassword_ReturnsDifferentHashes()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash1 = PasswordUtilities.HashPassword(password);
        var hash2 = PasswordUtilities.HashPassword(password);

        // Assert
        hash1.Should().NotBe(hash2); // Salt ensures different hashes
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = PasswordUtilities.HashPassword(password);

        // Act
        var isValid = PasswordUtilities.VerifyPassword(password, hash);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var correctPassword = "TestPassword123!";
        var wrongPassword = "WrongPassword456!";
        var hash = PasswordUtilities.HashPassword(correctPassword);

        // Act
        var isValid = PasswordUtilities.VerifyPassword(wrongPassword, hash);

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void HashPassword_WithNullOrEmpty_ThrowsArgumentException(string? password)
    {
        // Act & Assert
        var act = () => PasswordUtilities.HashPassword(password!);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void VerifyPassword_WithNullOrEmptyPassword_ReturnsFalse(string? password)
    {
        // Arrange
        var hash = PasswordUtilities.HashPassword("ValidPassword123!");

        // Act
        var isValid = PasswordUtilities.VerifyPassword(password!, hash);

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void VerifyPassword_WithNullOrEmptyHash_ReturnsFalse(string? hash)
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var isValid = PasswordUtilities.VerifyPassword(password, hash!);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void PasswordRoundTrip_MultipleTimes_WorksCorrectly()
    {
        // Arrange
        var passwords = new[] { "Test1!", "Test2@", "Test3#", "Test4$", "Test5%" };

        foreach (var password in passwords)
        {
            // Act
            var hash = PasswordUtilities.HashPassword(password);
            var isValid = PasswordUtilities.VerifyPassword(password, hash);

            // Assert
            isValid.Should().BeTrue($"Password '{password}' should verify against its hash");
        }
    }
}
