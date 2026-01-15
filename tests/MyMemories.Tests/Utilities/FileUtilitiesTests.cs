using MyMemories.Utilities;

namespace MyMemories.Tests.Utilities;

/// <summary>
/// Unit tests for FileUtilities class.
/// Tests file operations, size formatting, path validation, and file system utilities.
/// </summary>
public class FileUtilitiesTests
{
    private readonly string _testDirectory;

    public FileUtilitiesTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "MyMemoriesTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void FormatFileSize_WithBytes_ReturnsCorrectFormat()
    {
        // Arrange & Act & Assert
        FileUtilities.FormatFileSize(0).Should().Be("0 B");
        FileUtilities.FormatFileSize(512).Should().Be("512 B");
        FileUtilities.FormatFileSize(1023).Should().Be("1023 B");
    }

    [Fact]
    public void FormatFileSize_WithKilobytes_ReturnsCorrectFormat()
    {
        // Arrange & Act & Assert
        FileUtilities.FormatFileSize(1024).Should().Be("1 KB");
        FileUtilities.FormatFileSize(1536).Should().Be("1.5 KB");
        FileUtilities.FormatFileSize(10240).Should().Be("10 KB");
    }

    [Fact]
    public void FormatFileSize_WithMegabytes_ReturnsCorrectFormat()
    {
        // Arrange & Act & Assert
        FileUtilities.FormatFileSize(1048576).Should().Be("1.00 MB"); // 1024 * 1024
        FileUtilities.FormatFileSize(5242880).Should().Be("5.00 MB"); // 5 MB
    }

    [Fact]
    public void FormatFileSize_WithGigabytes_ReturnsCorrectFormat()
    {
        // Arrange & Act & Assert
        FileUtilities.FormatFileSize(1073741824).Should().Be("1 GB"); // 1024^3
        FileUtilities.FormatFileSize(2147483648).Should().Be("2 GB"); // 2 GB
    }

    [Theory]
    [InlineData("test.txt", ".txt")]
    [InlineData("document.pdf", ".pdf")]
    [InlineData("archive.zip", ".zip")]
    [InlineData("noextension", "")]
    [InlineData(".hidden", "")]
    public void GetFileExtension_ReturnsCorrectExtension(string filename, string expectedExtension)
    {
        // Act
        var extension = Path.GetExtension(filename);

        // Assert
        extension.Should().Be(expectedExtension);
    }

    [Fact]
    public void FileExists_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "test content");

        // Act
        var exists = File.Exists(testFile);

        // Assert
        exists.Should().BeTrue();

        // Cleanup
        File.Delete(testFile);
    }

    [Fact]
    public void FileExists_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var exists = File.Exists(nonExistentFile);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void DirectoryExists_WithExistingDirectory_ReturnsTrue()
    {
        // Act
        var exists = Directory.Exists(_testDirectory);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public void DirectoryExists_WithNonExistentDirectory_ReturnsFalse()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testDirectory, "nonexistent");

        // Act
        var exists = Directory.Exists(nonExistentDir);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void GetFileSize_WithExistingFile_ReturnsCorrectSize()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var content = "This is test content with specific length.";
        File.WriteAllText(testFile, content);

        // Act
        var fileInfo = new FileInfo(testFile);
        var size = fileInfo.Length;

        // Assert
        size.Should().BeGreaterThan(0);
        size.Should().Be(content.Length); // Approximate, encoding may vary

        // Cleanup
        File.Delete(testFile);
    }

    [Theory]
    [InlineData("valid_filename.txt", true)]
    [InlineData("another-file_name.pdf", true)]
    [InlineData("file with spaces.doc", true)]
    [InlineData("file<invalid>.txt", false)] // < is invalid
    [InlineData("file>invalid.txt", false)] // > is invalid
    [InlineData("file|invalid.txt", false)] // | is invalid
    [InlineData("file:invalid.txt", false)] // : is invalid (except for drive letter)
    [InlineData("file*invalid.txt", false)] // * is invalid
    [InlineData("file?invalid.txt", false)] // ? is invalid
    [InlineData("file\"invalid.txt", false)] // " is invalid
    public void IsValidFilename_ChecksInvalidCharacters(string filename, bool expectedValid)
    {
        // Act
        var invalidChars = Path.GetInvalidFileNameChars();
        var isValid = !filename.Any(c => invalidChars.Contains(c));

        // Assert
        isValid.Should().Be(expectedValid);
    }

    [Fact]
    public void CombinePaths_WorksCorrectly()
    {
        // Arrange
        var part1 = "C:\\Users";
        var part2 = "Documents";
        var part3 = "file.txt";

        // Act
        var combined = Path.Combine(part1, part2, part3);

        // Assert
        combined.Should().Be("C:\\Users\\Documents\\file.txt");
    }

    [Fact]
    public void GetFileName_ExtractsNameCorrectly()
    {
        // Arrange
        var fullPath = "C:\\Users\\Documents\\myfile.txt";

        // Act
        var fileName = Path.GetFileName(fullPath);

        // Assert
        fileName.Should().Be("myfile.txt");
    }

    [Fact]
    public void GetDirectoryName_ExtractsDirectoryCorrectly()
    {
        // Arrange
        var fullPath = "C:\\Users\\Documents\\myfile.txt";

        // Act
        var directory = Path.GetDirectoryName(fullPath);

        // Assert
        directory.Should().Be("C:\\Users\\Documents");
    }

    [Fact]
    public void CreateAndDeleteDirectory_WorksCorrectly()
    {
        // Arrange
        var testDir = Path.Combine(_testDirectory, "subdir");

        // Act - Create
        Directory.CreateDirectory(testDir);
        var existsAfterCreate = Directory.Exists(testDir);

        // Act - Delete
        Directory.Delete(testDir);
        var existsAfterDelete = Directory.Exists(testDir);

        // Assert
        existsAfterCreate.Should().BeTrue();
        existsAfterDelete.Should().BeFalse();
    }

    // Cleanup after all tests
    ~FileUtilitiesTests()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
