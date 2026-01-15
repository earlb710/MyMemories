# Quick Start Guide: Running Tests

## Prerequisites
- Visual Studio 2022 (or Visual Studio Code with C# extension)
- .NET 8 SDK installed

## Quick Commands

### Run All Tests
```bash
cd tests/MyMemories.Tests
dotnet test
```

### Run Tests with Detailed Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run Tests for Specific Class
```bash
# Run only PasswordUtilities tests
dotnet test --filter "FullyQualifiedName~PasswordUtilitiesTests"

# Run only CategoryService tests
dotnet test --filter "FullyQualifiedName~CategoryServiceTests"
```

### Run Single Test
```bash
dotnet test --filter "FullyQualifiedName~PasswordUtilitiesTests.HashPassword_WithValidPassword_ReturnsNonEmptyHash"
```

### Run Tests with Code Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Visual Studio

### Using Test Explorer
1. Open **Test ? Test Explorer** (Ctrl+E, T)
2. Click **Run All** (or right-click ? Run)
3. View results in the Test Explorer window

### Running Specific Tests
1. Open Test Explorer
2. Find the test you want to run
3. Right-click ? **Run** or **Debug**

### Viewing Code Coverage
1. **Analyze ? Code Coverage ? Run All Tests**
2. View results in Code Coverage Results window
3. Double-click on classes to see line-by-line coverage

## Visual Studio Code

### Install Extensions
```bash
# Install C# extension if not already installed
code --install-extension ms-dotnettools.csharp

# Install .NET Core Test Explorer
code --install-extension formulahendry.dotnet-test-explorer
```

### Run Tests
1. Open Test Explorer in sidebar
2. Click **Run All Tests** button
3. Or use Command Palette (Ctrl+Shift+P) ? ".NET: Run All Tests"

## First Time Setup

### 1. Verify Installation
```bash
# Check .NET SDK
dotnet --version

# Should show: 8.0.x or higher
```

### 2. Restore Packages
```bash
cd tests/MyMemories.Tests
dotnet restore
```

### 3. Build Tests
```bash
dotnet build
```

### 4. Run Tests
```bash
dotnet test
```

Expected output:
```
Starting test execution, please wait...
A total of 61 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    61, Skipped:     0, Total:    61, Duration: < 1 s
```

## Troubleshooting

### "dotnet test" not found
**Solution:** Install .NET 8 SDK from https://dotnet.microsoft.com/download

### Tests not discovered in Test Explorer
**Solution:**
1. Clean and rebuild solution (Build ? Clean Solution ? Build Solution)
2. Restart Visual Studio
3. Check that test project references are correct

### Tests failing with "Cannot find file"
**Solution:**
1. Check that test is using temporary directories
2. Ensure proper cleanup in `Dispose()` method
3. Check file paths are not hard-coded

### Tests are slow
**Solution:**
1. Check if tests are hitting actual file system (should use mocks)
2. Run specific test class instead of all tests
3. Parallelize tests if needed

## Test Results

### Interpreting Results
- **Green**: Test passed ?
- **Red**: Test failed ?
- **Yellow**: Test skipped (has `[Skip]` attribute) ??

### Common Failure Reasons
1. **Assertion failed**: Expected value doesn't match actual
2. **Exception thrown**: Code threw unexpected exception
3. **Timeout**: Test took too long
4. **Setup failed**: Test initialization failed

### Example Test Failure
```
Test Name:    HashPassword_WithValidPassword_ReturnsNonEmptyHash
Expected:     Not null or empty
Actual:       <empty string>
Stack Trace:  at PasswordUtilitiesTests.cs:line 15
```

**Fix:** Check PasswordUtilities.HashPassword() implementation

## Current Test Statistics

| Category | Tests | Status |
|----------|-------|--------|
| PasswordUtilities | 12 | ? Pass |
| FileUtilities | 15 | ? Pass |
| PathValidationUtilities | 11 | ? Pass |
| CategoryService | 10 | ? Pass |
| TreeViewService | 13 | ? Pass |
| **Total** | **61** | **? Pass** |

## Next Steps

1. Run tests: `dotnet test`
2. Check coverage: `dotnet test --collect:"XPlat Code Coverage"`
3. Add more tests as needed
4. Keep tests updated with code changes

## Getting Help

- Read `tests/README.md` for detailed documentation
- Read `tests/TEST-MAINTENANCE.md` for maintenance guide
- Check existing tests for patterns
- Ask team for code review

---

**Quick Tip:** Run tests before every commit!
```bash
dotnet test && git commit -m "Your commit message"
```
