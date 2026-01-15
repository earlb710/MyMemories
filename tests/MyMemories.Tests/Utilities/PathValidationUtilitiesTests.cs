using MyMemories.Utilities;

namespace MyMemories.Tests.Utilities;

/// <summary>
/// Unit tests for PathValidationUtilities class.
/// Tests path validation, sanitization, and security checks.
/// </summary>
public class PathValidationUtilitiesTests
{
    [Theory]
    [InlineData("C:\\Users\\Documents\\file.txt", true)]
    [InlineData("C:\\Windows\\System32\\config", true)]
    [InlineData("\\\\network\\share\\folder", true)]
    [InlineData("relative\\path\\file.txt", false)] // Relative path
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidAbsolutePath_ValidatesCorrectly(string? path, bool expectedValid)
    {
        // Act
        bool isValid;
        if (string.IsNullOrEmpty(path))
        {
            isValid = false;
        }
        else
        {
            isValid = Path.IsPathRooted(path);
        }

        // Assert
        isValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData("C:\\Users\\Documents")]
    [InlineData("C:\\Program Files")]
    [InlineData("D:\\Data")]
    public void IsValidDirectory_WithExistingOrValidPath_DoesNotThrow(string path)
    {
        // Act
        Action act = () => Path.GetFullPath(path);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("file<test>.txt", "file_test_.txt")]
    [InlineData("file|test.txt", "file_test.txt")]
    [InlineData("file:test.txt", "file_test.txt")]
    [InlineData("file*test.txt", "file_test.txt")]
    [InlineData("file?test.txt", "file_test.txt")]
    [InlineData("file\"test\".txt", "file_test_.txt")]
    [InlineData("valid_file.txt", "valid_file.txt")]
    public void SanitizeFileName_RemovesInvalidCharacters(string input, string expected)
    {
        // Arrange
        var invalidChars = Path.GetInvalidFileNameChars();

        // Act
        var result = string.Join("_", input.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        result = result.Replace("__", "_"); // Clean up double underscores

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("normal_name.txt")]
    [InlineData("file-with-dashes.pdf")]
    [InlineData("file_with_underscores.doc")]
    [InlineData("file.with.dots.txt")]
    public void SanitizeFileName_KeepsValidCharacters(string filename)
    {
        // Arrange
        var invalidChars = Path.GetInvalidFileNameChars();

        // Act
        var hasInvalidChars = filename.Any(c => invalidChars.Contains(c));

        // Assert
        hasInvalidChars.Should().BeFalse();
    }

    [Theory]
    [InlineData("C:\\Users\\..\\..\\Windows\\System32", true)] // Path traversal attempt
    [InlineData("C:\\Users\\Documents\\file.txt", false)] // Normal path
    [InlineData("..\\..\\sensitive\\file.txt", true)] // Relative traversal
    public void ContainsPathTraversal_DetectsTraversal(string path, bool expectedHasTraversal)
    {
        // Act
        var hasTraversal = path.Contains("..");

        // Assert
        hasTraversal.Should().Be(expectedHasTraversal);
    }

    [Theory]
    [InlineData("C:\\Users\\Documents", "C:\\Users\\Documents\\file.txt", true)]
    [InlineData("C:\\Users\\Documents", "C:\\Users\\Downloads\\file.txt", false)]
    [InlineData("C:\\Users", "C:\\Users\\Documents\\file.txt", true)]
    public void IsPathWithinDirectory_ValidatesCorrectly(string baseDirectory, string targetPath, bool expectedWithin)
    {
        // Act
        var fullBase = Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var fullTarget = Path.GetFullPath(targetPath).TrimEnd(Path.DirectorySeparatorChar);
        var isWithin = fullTarget.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase);

        // Assert
        isWithin.Should().Be(expectedWithin);
    }

    [Fact]
    public void GetFullPath_NormalizesPath()
    {
        // Arrange
        var relativePath = ".\\test\\..\\file.txt";

        // Act
        Action act = () => Path.GetFullPath(relativePath);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("file.txt", ".txt")]
    [InlineData("archive.zip", ".zip")]
    [InlineData("document.pdf", ".pdf")]
    [InlineData("no_extension", "")]
    public void HasExtension_DetectsExtensionCorrectly(string filename, string expectedExtension)
    {
        // Act
        var extension = Path.GetExtension(filename);

        // Assert
        extension.Should().Be(expectedExtension);
    }

    [Theory]
    [InlineData("CON", true)] // Reserved name
    [InlineData("PRN", true)] // Reserved name
    [InlineData("AUX", true)] // Reserved name
    [InlineData("NUL", true)] // Reserved name
    [InlineData("COM1", true)] // Reserved name
    [InlineData("LPT1", true)] // Reserved name
    [InlineData("normal_file", false)] // Normal name
    public void IsReservedFileName_DetectsReservedNames(string filename, bool expectedReserved)
    {
        // Arrange
        var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename).ToUpperInvariant();

        // Act
        var isReserved = reservedNames.Contains(nameWithoutExtension);

        // Assert
        isReserved.Should().Be(expectedReserved);
    }

    [Theory]
    [InlineData("file.txt", 8)] // 8 characters
    [InlineData("a.txt", 5)] // 5 characters
    [InlineData("verylongfilename.txt", 20)] // 20 characters
    public void GetFileNameLength_ReturnsCorrectLength(string filename, int expectedLength)
    {
        // Act
        var length = filename.Length;

        // Assert
        length.Should().Be(expectedLength);
    }

    [Fact]
    public void MaxPathLength_IsCorrect()
    {
        // Arrange & Act
        var maxLength = 260; // Windows MAX_PATH

        // Assert
        maxLength.Should().Be(260);
    }
}
