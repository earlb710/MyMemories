# MyMemories Test Suite

This directory contains the unit test suite for the MyMemories application.

## Structure

```
tests/
??? MyMemories.Tests/
    ??? Utilities/
    ?   ??? PasswordUtilitiesTests.cs
    ?   ??? FileUtilitiesTests.cs
    ?   ??? PathValidationUtilitiesTests.cs
    ??? Services/
    ?   ??? CategoryServiceTests.cs
    ?   ??? TreeViewServiceTests.cs
    ?   ??? [More service tests to be added]
    ??? MyMemories.Tests.csproj
```

## Testing Framework

- **xUnit**: Primary testing framework
- **Moq**: Mocking framework for dependencies
- **FluentAssertions**: Fluent assertion library for readable tests

## Running Tests

### Visual Studio
1. Open Test Explorer (Test ? Test Explorer)
2. Click "Run All" or run specific tests

### Command Line
```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Test Coverage Goals

- **Target**: 70%+ code coverage for business logic
- **Priority Areas**:
  - Utilities (Password, File, Path validation)
  - Services (Category, TreeView, Details)
  - Import/Export functionality
  - Data validation and transformation

## Writing Tests

### Test Naming Convention
```csharp
public void MethodName_Scenario_ExpectedResult()
{
    // Arrange
    
    // Act
    
    // Assert
}
```

### Example Test
```csharp
[Fact]
public void HashPassword_WithValidPassword_ReturnsNonEmptyHash()
{
    // Arrange
    var password = "TestPassword123!";

    // Act
    var hash = PasswordUtilities.HashPassword(password);

    // Assert
    hash.Should().NotBeNullOrEmpty();
}
```

### Using Theory for Multiple Test Cases
```csharp
[Theory]
[InlineData("Weak1!", false)]
[InlineData("StrongPassword123!", true)]
public void ValidatePasswordStrength_ChecksComplexity(string password, bool expected)
{
    // Act
    var result = PasswordUtilities.ValidatePasswordStrength(password);

    // Assert
    result.Should().Be(expected);
}
```

## Test Categories

### Unit Tests
- Test individual methods in isolation
- Mock external dependencies
- Fast execution
- Located in: `MyMemories.Tests/`

### Integration Tests (Future)
- Test multiple components together
- Test file I/O operations
- Test database operations
- To be located in: `MyMemories.IntegrationTests/`

## Mocking Guidelines

### When to Mock
- External dependencies (file system, network)
- UI components (TreeView, MainWindow)
- Configuration services
- Time-dependent operations

### When NOT to Mock
- Simple data objects (CategoryItem, LinkItem)
- Pure utility functions
- Immutable structures

### Example Mock
```csharp
var mockTreeView = new Mock<TreeView>();
mockTreeView.Setup(tv => tv.RootNodes).Returns(new TreeViewNodeCollection());
```

## Test Data

### Use Temporary Directories
```csharp
private readonly string _testDirectory;

public MyTests()
{
    _testDirectory = Path.Combine(Path.GetTempPath(), "MyMemoriesTests", Guid.NewGuid().ToString());
    Directory.CreateDirectory(_testDirectory);
}

public void Dispose()
{
    if (Directory.Exists(_testDirectory))
    {
        Directory.Delete(_testDirectory, recursive: true);
    }
}
```

### Test File Naming
- Use descriptive names
- Use GUID for uniqueness
- Clean up after tests

## Continuous Integration

Tests are run automatically on:
- Every commit to main branch
- Every pull request
- Nightly builds

## Current Test Status

### ? Completed
- PasswordUtilities (12 tests)
- FileUtilities (15 tests)
- PathValidationUtilities (11 tests)
- CategoryService (10 tests)
- TreeViewService (13 tests)

### ?? In Progress
- DetailsViewService
- TagManagementService
- RatingManagementService

### ?? Planned
- BookmarkImporterService
- BookmarkExporterService
- CategoryImportService
- UrlStateCheckerService
- CatalogService
- BackupService
- And more...

## Maintaining Tests

### Keep Tests Updated
- Update tests when code changes
- Add tests for new features
- Remove tests for removed features
- Refactor tests when refactoring code

### Test Maintenance Checklist
- [ ] Test names are descriptive
- [ ] Tests are independent
- [ ] Tests clean up after themselves
- [ ] Tests use FluentAssertions
- [ ] Tests follow AAA pattern (Arrange, Act, Assert)
- [ ] Tests are fast (<100ms each)
- [ ] Tests have clear failure messages

## Troubleshooting

### Tests Failing Locally
1. Clean and rebuild solution
2. Check for file locks (close VS, delete bin/obj folders)
3. Verify test data directory permissions
4. Check for timing issues (add delays if needed)

### Tests Passing Locally but Failing in CI
1. Check for hard-coded paths
2. Check for time zone dependencies
3. Check for file system case sensitivity
4. Check for missing test data files

## Resources

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [.NET Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

## Contributing

When adding tests:
1. Follow existing patterns and conventions
2. Add tests for both success and failure scenarios
3. Use descriptive test names
4. Add comments for complex test logic
5. Update this README if adding new test categories
