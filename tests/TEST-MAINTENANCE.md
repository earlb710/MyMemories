# Test Maintenance Guide

## Purpose
This document outlines how to keep the test suite synchronized with code changes in the MyMemories application.

## Quick Reference

### When Code Changes
| Change Type | Action Required | Priority |
|------------|----------------|----------|
| Add new method | Add test | High |
| Modify method signature | Update affected tests | High |
| Rename method | Update test names | Medium |
| Delete method | Delete test | High |
| Add new class | Create test class | High |
| Refactor code | Review and update tests | Medium |
| Bug fix | Add regression test | High |
| New feature | Add feature tests | High |

## Test Update Workflow

### 1. Adding New Code
```
Code Added ? Create Test File ? Write Tests ? Run Tests ? Commit Both
```

**Example:**
```csharp
// New service method
public void ValidateEmail(string email)
{
    // implementation
}

// New test
[Theory]
[InlineData("valid@email.com", true)]
[InlineData("invalid", false)]
public void ValidateEmail_ChecksFormat(string email, bool expected)
{
    // test implementation
}
```

### 2. Modifying Existing Code
```
Code Modified ? Find Related Tests ? Update Tests ? Verify Passes ? Commit Both
```

**Steps:**
1. Use "Find All References" on the method
2. Locate tests in `MyMemories.Tests`
3. Update test assertions/mocks
4. Run tests to verify
5. Commit code and test changes together

### 3. Refactoring Code
```
Plan Refactor ? Review Tests ? Refactor Code ? Update Tests ? Run Full Suite
```

**Checklist:**
- [ ] Identify all tests affected by refactor
- [ ] Keep test intent the same
- [ ] Update only what's necessary
- [ ] Ensure all tests still pass
- [ ] Check code coverage hasn't decreased

## Common Scenarios

### Scenario 1: Adding a New Utility Method
**Location:** `MyMemories/Utilities/StringUtilities.cs`

**Steps:**
1. Create method in utility class
2. Open/create `tests/MyMemories.Tests/Utilities/StringUtilitiesTests.cs`
3. Add test methods
4. Run tests
5. Commit both files

**Template:**
```csharp
// Utilities/StringUtilities.cs
public static class StringUtilities
{
    public static string TruncateWithEllipsis(string text, int maxLength)
    {
        // implementation
    }
}

// Tests/Utilities/StringUtilitiesTests.cs
public class StringUtilitiesTests
{
    [Theory]
    [InlineData("Short", 10, "Short")]
    [InlineData("Very long text", 10, "Very lo...")]
    public void TruncateWithEllipsis_WorksCorrectly(string input, int maxLength, string expected)
    {
        var result = StringUtilities.TruncateWithEllipsis(input, maxLength);
        result.Should().Be(expected);
    }
}
```

### Scenario 2: Modifying a Service Method
**Location:** `MyMemories/Services/CategoryService.cs`

**Steps:**
1. Modify method signature or behavior
2. Open `tests/MyMemories.Tests/Services/CategoryServiceTests.cs`
3. Find tests for that method (search by method name)
4. Update test setup, mocks, or assertions
5. Run affected tests
6. Commit both files

**Example:**
```csharp
// BEFORE: Method takes one parameter
public void SaveCategory(CategoryItem category)

// Test BEFORE
[Fact]
public void SaveCategory_WithValidCategory_Succeeds()
{
    var category = new CategoryItem { Name = "Test" };
    _service.SaveCategory(category);
    // assertions
}

// AFTER: Method takes additional parameter
public void SaveCategory(CategoryItem category, bool validateBeforeSave)

// Test AFTER - Updated
[Theory]
[InlineData(true)]
[InlineData(false)]
public void SaveCategory_WithValidCategory_Succeeds(bool validateBeforeSave)
{
    var category = new CategoryItem { Name = "Test" };
    _service.SaveCategory(category, validateBeforeSave);
    // assertions
}
```

### Scenario 3: Adding a Bug Fix
**Location:** Any

**Steps:**
1. Write a failing test that reproduces the bug
2. Fix the bug
3. Verify test now passes
4. Commit test and fix together

**Example:**
```csharp
// Bug: HashPassword throws on empty string instead of returning gracefully

// 1. Add failing test
[Fact]
public void HashPassword_WithEmptyString_ThrowsArgumentException()
{
    var act = () => PasswordUtilities.HashPassword("");
    act.Should().Throw<ArgumentException>();
}

// 2. Run test - it should FAIL
// 3. Fix the code
public static string HashPassword(string password)
{
    if (string.IsNullOrEmpty(password))
        throw new ArgumentException("Password cannot be empty");
    // rest of implementation
}

// 4. Run test - it should PASS
```

### Scenario 4: Refactoring a Class
**Location:** `MyMemories/Services/CategoryService.cs`

**Example: Extracting a method**
```csharp
// BEFORE
public async Task SaveCategoryAsync(TreeViewNode node)
{
    // Validation logic here (10 lines)
    // Save logic here (20 lines)
}

// AFTER
public async Task SaveCategoryAsync(TreeViewNode node)
{
    ValidateCategoryNode(node);
    await SaveCategoryToFileAsync(node);
}

private void ValidateCategoryNode(TreeViewNode node)
{
    // Validation logic
}

private async Task SaveCategoryToFileAsync(TreeViewNode node)
{
    // Save logic
}

// Tests remain the same (testing public interface)
[Fact]
public async Task SaveCategoryAsync_WithValidNode_Succeeds()
{
    // Same test, still works
}

// Add new test for edge cases if needed
[Fact]
public void ValidateCategoryNode_WithInvalidNode_ThrowsException()
{
    // New test if validation is complex
}
```

## Test Organization

### Directory Structure Mirror
```
MyMemories/
??? Utilities/
?   ??? PasswordUtilities.cs
?   ??? FileUtilities.cs
??? Services/
    ??? CategoryService.cs
    ??? TreeViewService.cs

tests/MyMemories.Tests/
??? Utilities/
?   ??? PasswordUtilitiesTests.cs      // Mirrors PasswordUtilities.cs
?   ??? FileUtilitiesTests.cs          // Mirrors FileUtilities.cs
??? Services/
    ??? CategoryServiceTests.cs        // Mirrors CategoryService.cs
    ??? TreeViewServiceTests.cs        // Mirrors TreeViewService.cs
```

### Naming Convention
| Source File | Test File |
|------------|-----------|
| `CategoryService.cs` | `CategoryServiceTests.cs` |
| `PasswordUtilities.cs` | `PasswordUtilitiesTests.cs` |
| `LinkItem.cs` | `LinkItemTests.cs` |

## Automation Checklist

### Pre-Commit Checklist
- [ ] All modified code files have corresponding test updates
- [ ] New code has new tests
- [ ] All tests pass locally
- [ ] Code coverage hasn't significantly decreased
- [ ] Test names are descriptive
- [ ] Tests are independent (can run in any order)

### Pull Request Checklist
- [ ] Tests are included for all code changes
- [ ] Tests follow existing patterns
- [ ] Tests are well documented
- [ ] No test files are missing
- [ ] CI pipeline passes

### Code Review Checklist
- [ ] Test coverage is adequate
- [ ] Tests actually test the intended behavior
- [ ] Edge cases are covered
- [ ] Error cases are tested
- [ ] Test names clearly describe what they test

## Tools for Test Maintenance

### Finding Related Tests
```bash
# Find test file for a class
# If editing: MyMemories/Services/CategoryService.cs
# Open: tests/MyMemories.Tests/Services/CategoryServiceTests.cs
```

### Running Specific Tests
```bash
# Run tests for one class
dotnet test --filter "FullyQualifiedName~CategoryServiceTests"

# Run one specific test
dotnet test --filter "FullyQualifiedName~CategoryServiceTests.SaveCategory_WithValidCategory_Succeeds"
```

### Code Coverage
```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"

# View coverage in Visual Studio
# Analyze ? Code Coverage ? Run All Tests
```

## Anti-Patterns to Avoid

### ? Don't Do This
```csharp
// Writing code without tests
public void NewMethod() { /* code */ }
// Commit without adding test

// Modifying tests to make them pass
[Fact]
public void Test_ShouldWork()
{
    // Modified test to match broken behavior
    result.Should().Be("WRONG"); // Making test pass incorrectly
}

// Ignoring failing tests
[Fact(Skip = "Fails sometimes")]
public void UnstableTest() { }

// Using hard-coded paths
var file = @"C:\MyHardCodedPath\test.txt";

// Not cleaning up test data
public void Test()
{
    File.WriteAllText("test.txt", "data");
    // No cleanup - file stays after test
}
```

### ? Do This Instead
```csharp
// Write test first or alongside code
public void NewMethod() { /* code */ }
// Create test immediately

// Fix broken code, not tests
[Fact]
public void Test_ShouldWork()
{
    // Keep test expectations correct
    result.Should().Be("CORRECT");
}
// If test fails, fix the code

// Investigate and fix flaky tests
[Fact] // Remove Skip, fix the test
public void StableTest() 
{
    // Make test deterministic
}

// Use temporary paths
var file = Path.Combine(Path.GetTempPath(), "test.txt");

// Always clean up
public void Test()
{
    var file = Path.Combine(_testDir, "test.txt");
    File.WriteAllText(file, "data");
    // Cleanup in Dispose() or try/finally
}
```

## Continuous Improvement

### Regular Maintenance Tasks
- **Weekly**: Review failed tests, fix flaky tests
- **Monthly**: Check code coverage, identify untested areas
- **Quarterly**: Refactor test code, update test patterns
- **Annually**: Review test architecture, update tools

### Metrics to Track
- Test count
- Code coverage percentage
- Test execution time
- Flaky test count
- Tests per code file ratio

### Red Flags
- ?? Coverage dropping below 70%
- ?? Tests taking >10 seconds
- ?? Tests failing intermittently
- ?? Many tests marked as `[Skip]`
- ?? Code changes without test updates

## Getting Help

### Resources
1. Check existing tests for patterns
2. Read `tests/README.md` for guidelines
3. Ask team members for review
4. Reference test documentation in this folder

### Common Questions

**Q: Should I test private methods?**
A: No, test through public interface. If private method needs testing, consider making it internal with `[InternalsVisibleTo]`.

**Q: How much mocking is too much?**
A: If mocking setup is longer than the test, consider integration test or simplify design.

**Q: What if test is too slow?**
A: Use mocks for I/O, move to integration tests if needed, or parallelize tests.

**Q: Should I test getters/setters?**
A: Only if they have logic. Simple property accessors don't need tests.

## Summary

**Golden Rule:** Code and tests change together. Never commit one without the other.

**Quick Command:**
```bash
# Before committing
dotnet test
# All tests should pass
```

Keep this guide handy and refer to it whenever making code changes!
