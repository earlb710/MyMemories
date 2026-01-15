# MyMemories - Improvement Roadmap

Last Updated: 2026-01-14

## ?? Priority Matrix

- ?? **Critical** - High impact, needed soon
- ?? **Important** - High impact, can be scheduled
- ?? **Nice to Have** - Lower impact, backlog
- ?? **Research Needed** - Needs investigation first

---

## ??? Architecture & Organization

### ?? Refactor MainWindow Partial Classes
**Current State:** MainWindow has many partial classes (DragDrop, Import, Config, TreeView, etc.)

**Improvements:**
- [ ] Evaluate which partials can become separate ViewModels
- [ ] Implement MVVM pattern for at least the main view
- [ ] Move business logic to service layer
- [ ] Reduce MainWindow responsibilities to coordination only

**Estimated Effort:** 2-3 weeks  
**Benefits:** Better testability, cleaner separation of concerns

### ?? Implement Service Interfaces
**Current State:** Services are concrete classes with no abstraction layer

**Tasks:**
- [ ] Create `IDetailsViewService` interface
- [ ] Create `ICategoryService` interface
- [ ] Create `ITreeViewService` interface
- [ ] Create interfaces for all major services
- [ ] Implement dependency injection container
- [ ] Update MainWindow to use DI

**Estimated Effort:** 1 week  
**Benefits:** Better testability, easier mocking, cleaner dependencies

### ?? Split Large Services
**Current State:** Some services (CategoryService) handle too many responsibilities

**Tasks:**
- [ ] Review CategoryService responsibilities
- [ ] Split into focused services (CategoryPersistence, CategoryValidation, etc.)
- [ ] Review other services for similar issues
- [ ] Document service boundaries

**Estimated Effort:** 1 week  
**Benefits:** Easier maintenance, better SRP compliance

---

## ?? Testing & Quality

### ?? Add Unit Testing Infrastructure
**Current State:** No unit tests exist

**Tasks:**
- [ ] Add xUnit test project to solution
- [ ] Install Moq or NSubstitute for mocking
- [ ] Create test fixtures for core services
- [ ] Add tests for `PasswordUtilities`
- [ ] Add tests for `FileUtilities`
- [ ] Add tests for `PathValidationUtilities`
- [ ] Add tests for `CategoryService` core logic
- [ ] Add tests for import/export functionality
- [ ] Target 70%+ code coverage for business logic

**Estimated Effort:** 2 weeks initial setup + ongoing  
**Benefits:** Prevent regressions, enable confident refactoring

### ? COMPLETED: Improve Error Handling
**Completed:** 2026-01-14

**Implementation:**
- ? Created `GlobalExceptionHandler` service
- ? Added exception hooks in `App.xaml.cs` (UI, Task, AppDomain)
- ? Integrated with `ErrorLogService` for logging
- ? Created user-friendly error dialogs with technical details expander
- ? Added `ExceptionHandlingExtensions` for consistent error handling
- ? Created comprehensive documentation in `EXCEPTION-HANDLING-SYSTEM.md`
- ? Updated `MainWindow.InitializeAsync` to use global handler

**Benefits Achieved:**
- All unhandled exceptions are now caught and logged
- Users see friendly error messages instead of crashes
- Technical details available via expander for advanced users
- Consistent error handling strategy across the application
- Critical exceptions properly identified

**Estimated Effort:** 1 week ? **Actual: 1 day**

---

### ?? Improve Error Handling (LEGACY - SEE ABOVE)
**Current State:** Mix of empty catch blocks and proper error handling

**Tasks:**
- [x] Audit all empty `catch` blocks
- [x] Add specific exception types where appropriate
- [x] Implement global exception handler
- [ ] Add retry logic for file operations
- [ ] Add retry logic for network operations
- [ ] Document error handling strategy
- [ ] Add user-friendly error messages

**Estimated Effort:** 1 week  
**Benefits:** Better reliability, easier debugging

### ?? Add Integration Tests
**Current State:** No integration tests

**Tasks:**
- [ ] Create integration test project
- [ ] Test category load/save cycles
- [ ] Test import/export workflows
- [ ] Test catalog creation/refresh
- [ ] Test backup/restore functionality

**Estimated Effort:** 1 week  
**Benefits:** Catch integration issues early

---

## ?? Performance

### ?? Optimize Startup Auto-Refresh
**Current State:** Blocks UI thread during startup for catalog refreshes

**File:** `MainWindow.xaml.cs` - `CheckAllFoldersForChangesAsync()`

**Tasks:**
- [ ] Move auto-refresh to background thread
- [ ] Add cancellation support
- [ ] Show non-blocking progress indicator
- [ ] Cache folder states to avoid repeated checks
- [ ] Consider lazy loading for large catalogs

**Estimated Effort:** 2-3 days  
**Benefits:** Faster startup, better UX

### ?? Optimize TreeView Operations
**Current State:** Multiple iterations, recursive algorithms

**Files:** `MainWindow.xaml.cs`, `TreeViewService.cs`

**Tasks:**
- [ ] Refactor `RemoveInvalidNodes()` to single pass
- [ ] Convert `CollectAutoRefreshCatalogs()` to iterative
- [ ] Add TreeView virtualization for large hierarchies
- [ ] Profile TreeView performance with 10,000+ nodes
- [ ] Optimize node search algorithms

**Estimated Effort:** 1 week  
**Benefits:** Better performance with large datasets

### ?? Implement Large File Streaming
**Current State:** Entire text files loaded into memory

**File:** `DetailsViewService.cs`

**Tasks:**
- [ ] Add file size check before loading
- [ ] Implement streaming reader for files >1MB
- [ ] Add pagination for very large files
- [ ] Show "file too large" warning with options

**Estimated Effort:** 3-4 days  
**Benefits:** Handle larger files without memory issues

### ?? Add Image Thumbnail Caching
**Current State:** Full images loaded each time

**Tasks:**
- [ ] Generate thumbnails for image files
- [ ] Cache thumbnails in temp folder
- [ ] Implement LRU cache for thumbnails
- [ ] Add cache cleanup on exit

**Estimated Effort:** 2-3 days  
**Benefits:** Faster image loading, less memory usage

### ?? Profile Memory Usage
**Research:** Investigate potential memory leaks

**Tasks:**
- [ ] Profile app with Visual Studio Diagnostics
- [ ] Check WebView2 disposal
- [ ] Check large object retention
- [ ] Add memory usage monitoring
- [ ] Document memory usage patterns

**Estimated Effort:** 1 week  
**Benefits:** Identify and fix memory leaks

---

## ?? UI/UX

### ?? Document TextBox Selection Bug
**Current State:** Known WinUI3 issue with selection background persistence

**File:** `MainWindow.TextSearch.cs`, `DetailsViewService.cs`

**Tasks:**
- [ ] Add comment in code explaining the limitation
- [ ] Add to user documentation/FAQ
- [ ] Consider filing issue with Microsoft WinUI team
- [ ] Research alternative controls (custom RichTextBlock?)

**Estimated Effort:** 1 hour documentation + research  
**Benefits:** Set proper user expectations

### ?? Improve Accessibility
**Current State:** Limited accessibility support

**Tasks:**
- [ ] Add `AutomationProperties.Name` to all interactive elements
- [ ] Test with Windows Narrator screen reader
- [ ] Add keyboard shortcuts documentation
- [ ] Improve keyboard navigation in search panel
- [ ] Ensure proper tab order
- [ ] Add high contrast theme support
- [ ] Test with screen magnifier

**Estimated Effort:** 1 week  
**Benefits:** Better accessibility for all users

### ?? Test Responsive Design
**Current State:** Likely has issues on different DPIs/sizes

**Tasks:**
- [ ] Test on 4K displays
- [ ] Test on 125%, 150%, 200% DPI scaling
- [ ] Test with different window sizes
- [ ] Remove hardcoded sizes where possible
- [ ] Use relative sizing
- [ ] Test on small screens (1366x768)

**Estimated Effort:** 2-3 days  
**Benefits:** Better experience across devices

---

## ?? Security

### ?? Enhance Password Security
**Current State:** Good hashing, but room for improvement

**File:** `PasswordUtilities.cs`, `MainWindow.Password.cs`

**Tasks:**
- [ ] Research `SecureString` for in-memory storage
- [ ] Implement password string clearing after use
- [ ] Add password strength validation
- [ ] Add password complexity requirements (optional)
- [ ] Consider adding salt to password hashes
- [ ] Add password change functionality

**Estimated Effort:** 3-4 days  
**Benefits:** Better security for sensitive data

### ?? Strengthen Path Validation
**Current State:** PathValidationUtilities exists but may not be consistently used

**File:** `PathValidationUtilities.cs`

**Tasks:**
- [ ] Audit all user path inputs
- [ ] Ensure all paths go through validation
- [ ] Add directory traversal attack prevention
- [ ] Add validation to catalog operations
- [ ] Add validation to import/export operations
- [ ] Add unit tests for path validation

**Estimated Effort:** 2-3 days  
**Benefits:** Prevent security vulnerabilities

---

## ?? Code Quality

### ?? Fix Async/Await Patterns
**Current State:** Fire-and-forget initialization is risky

**File:** `MainWindow.xaml.cs` - Constructor

**Issue:**
```csharp
_ = InitializeAsync(); // Fire and forget - risky
```

**Tasks:**
- [ ] Add proper exception handling to `InitializeAsync()`
- [ ] Handle initialization failures gracefully
- [ ] Add `TaskScheduler.UnobservedTaskException` handler
- [ ] Review all fire-and-forget async calls
- [ ] Add `ConfigureAwait(false)` to library code

**Estimated Effort:** 1 day  
**Benefits:** Prevent silent failures, better error handling

### ?? Replace Magic Numbers
**Current State:** Many unnamed constants in code

**Examples:**
```csharp
await Task.Delay(100); // What's this delay for?
const int maxDepth = 100; // Why 100?
```

**Tasks:**
- [ ] Extract all magic numbers to named constants
- [ ] Add XML documentation explaining constants
- [ ] Group related constants in static classes
- [ ] Consider configuration for some values

**Estimated Effort:** 2-3 days  
**Benefits:** Better code readability and maintainability

### ?? Use StringBuilder for String Building
**Current State:** String concatenation in loops

**Tasks:**
- [ ] Audit string concatenation in loops
- [ ] Replace with `StringBuilder` where appropriate
- [ ] Profile performance improvement
- [ ] Use `Path.Combine` for all path building

**Estimated Effort:** 1-2 days  
**Benefits:** Better performance, cleaner code

---

## ?? Documentation

### ?? Complete XML Documentation
**Current State:** Incomplete XML docs on public APIs

**Tasks:**
- [ ] Add `<param>` tags to all public methods
- [ ] Add `<returns>` tags to all public methods
- [ ] Document async/threading behavior
- [ ] Document side effects
- [ ] Document exceptions that can be thrown
- [ ] Enable XML documentation file generation
- [ ] Generate API documentation

**Estimated Effort:** 1 week  
**Benefits:** Better IntelliSense, easier for contributors

### ?? Add Code Comments for Complex Logic
**Current State:** Some complex algorithms lack explanation

**Areas Needing Comments:**
- [ ] Auto-refresh logic in `CheckAllFoldersForChangesAsync()`
- [ ] Zip catalog handling in `ZipCatalogService.cs`
- [ ] Bookmark import/export transformations
- [ ] TreeView node traversal algorithms
- [ ] Password encryption/decryption flow

**Estimated Effort:** 2-3 days  
**Benefits:** Easier maintenance, knowledge transfer

### ?? Create Developer Documentation
**Current State:** No developer onboarding docs

**Tasks:**
- [ ] Create `docs/DEVELOPER.md`
- [ ] Document architecture overview
- [ ] Document data flow
- [ ] Document service responsibilities
- [ ] Create contribution guidelines
- [ ] Add troubleshooting guide
- [ ] Document build process

**Estimated Effort:** 3-4 days  
**Benefits:** Easier onboarding, consistent contributions

---

## ?? Technical Debt

### ? COMPLETED: Extract Dialog Builders & Duplicate Code Patterns
**Completed:** 2026-01-14

**Implementation:**
- ? Created `DialogFactory` utility class with 12+ dialog methods
- ? Centralized confirmation, error, info, warning, success dialogs
- ? Added text input, password, list selection, and progress dialogs
- ? Created `TreeViewTraversalUtilities` with 20+ traversal methods
- ? Centralized depth-first, breadth-first, find, filter operations
- ? Created `ErrorMessageFormatter` with 15+ formatting methods
- ? Centralized file, network, validation, import/export error messages
- ? Created comprehensive documentation in `CODE-DEDUPLICATION-GUIDE.md`
- ? **Started opportunistic migration** - MainWindow.Helpers.cs (3 dialogs, 15 lines saved)
- ? Created `DIALOG-MIGRATION-TRACKER.md` to track migration progress

**Benefits Achieved:**
- Reduced dialog creation code by 70-80% (estimated 500+ lines saved)
- Eliminated 5-7 duplicate tree traversal functions
- Consistent error messaging across the application
- Easier maintenance - update once instead of many places
- Better testability - utilities can be unit tested
- Improved user experience - consistent dialogs and messages

**Ongoing (Opportunistic Migration):**
- ? Migrate existing dialogs as files are modified
- ?? Progress: 3/60 dialogs = 5% (Target: 20% = 15 dialogs)
- ?? Tracking: See `docs/DIALOG-MIGRATION-TRACKER.md`

**Estimated Effort:** 2-3 days ? **Actual: 1 day**

**Next Steps (Opportunistic):**
- [ ] Migrate MainWindow.ContextMenu.Category.cs (15+ dialogs estimated)
- [ ] Migrate MainWindow.ContextMenu.Link.cs (12+ dialogs estimated)
- [ ] Continue migration as code is modified
- [ ] Update tracker document with each migration

---

### ?? Extract Dialog Builders (LEGACY - COMPLETED ABOVE)
**Current State:** Dialog creation code duplicated

**Tasks:**
- [x] Create `DialogFactory` utility class
- [x] Extract common dialog patterns
- [x] Centralize error message formatting
- [x] Create reusable dialog templates
- [ ] Update all dialog creation code (ongoing as code is modified)

**Estimated Effort:** 2-3 days  
**Benefits:** Less duplication, consistent UX

### ?? Refactor TreeView Traversal (LEGACY - COMPLETED ABOVE)
**Current State:** Node traversal logic duplicated

**File:** Multiple files with recursive traversal

**Tasks:**
- [ ] Create `TreeViewTraversalUtilities` class
- [ ] Add common traversal methods (BFS, DFS)
- [ ] Add query methods (FindNode, FilterNodes)
- [ ] Replace duplicated traversal code
- [ ] Add unit tests

**Estimated Effort:** 2-3 days  
**Benefits:** Less duplication, easier to test

### ?? Split ConfigurationService
**Current State:** Mixes directories, passwords, logging concerns

**File:** `ConfigurationService.cs`

**Tasks:**
- [ ] Create `DirectoryConfigurationService`
- [ ] Create `SecurityConfigurationService`
- [ ] Create `LoggingConfigurationService`
- [ ] Update dependencies
- [ ] Update tests

**Estimated Effort:** 2-3 days  
**Benefits:** Better separation of concerns

### ?? Implement Structured Logging
**Current State:** Mix of `Debug.WriteLine` and structured logging

**Tasks:**
- [ ] Evaluate logging frameworks (Serilog, NLog)
- [ ] Install chosen framework
- [ ] Create logging configuration
- [ ] Replace `Debug.WriteLine` calls
- [ ] Add log levels (Debug, Info, Warning, Error)
- [ ] Add log filtering
- [ ] Add log file rotation

**Estimated Effort:** 3-4 days  
**Benefits:** Better diagnostics, production monitoring

---

## ?? Quick Wins (High Impact, Low Effort)

### ? 1. Add XML Documentation to Public APIs
**Estimated Time:** 1-2 hours  
**Impact:** Immediate IntelliSense improvement

**Tasks:**
- [ ] Document all public methods in Services folder
- [ ] Document all public properties in Models folder
- [ ] Enable XML doc warnings in project settings

---

### ? 2. Extract Dialog Builders to Utilities
**Estimated Time:** 2-3 hours  
**Impact:** Reduce code duplication significantly

**Tasks:**
- [ ] Create `DialogFactory.cs`
- [ ] Move common patterns to factory
- [ ] Update 3-5 most duplicated dialogs as proof of concept

---

### ? 3. Add Input Validation to Entry Points
**Estimated Time:** 3-4 hours  
**Impact:** Prevent crashes and security issues

**Tasks:**
- [ ] Audit all TextBox inputs
- [ ] Add validation to file path inputs
- [ ] Add validation to import operations
- [ ] Add validation to URL inputs

---

### ? 4. Create Unit Tests for Utilities
**Estimated Time:** 2-3 hours  
**Impact:** Immediate test coverage for critical code

**Tasks:**
- [ ] Test `PasswordUtilities.HashPassword()`
- [ ] Test `PathValidationUtilities` methods
- [ ] Test `FileUtilities` methods
- [ ] Test import/export models

---

### ? 5. Add ConfigureAwait(false) to Library Code
**Estimated Time:** 1 hour  
**Impact:** Better performance in service layer

**Tasks:**
- [ ] Add to all service layer async methods
- [ ] Add to utility async methods
- [ ] Skip for UI event handlers (keep context)

---

## ?? Metrics to Track

### Code Quality Metrics
- [ ] Set up code coverage reporting (target: 70%+)
- [ ] Track cyclomatic complexity (target: <15 per method)
- [ ] Track method length (target: <50 lines)
- [ ] Track class size (target: <500 lines)

### Performance Metrics
- [ ] Track startup time (target: <3 seconds)
- [ ] Track search response time (target: <500ms)
- [ ] Track catalog refresh time
- [ ] Track memory usage over time

### Quality Gates
- [ ] All public APIs must have XML documentation
- [ ] No new code without unit tests
- [ ] All async methods must handle exceptions
- [ ] All user inputs must be validated

---

## ??? Suggested Implementation Order

### Phase 1: Foundation (Weeks 1-2)
1. Add unit testing infrastructure
2. Fix async/await patterns
3. Add XML documentation to existing code
4. Extract dialog builders

### Phase 2: Architecture (Weeks 3-5)
1. Implement service interfaces
2. Add dependency injection
3. Refactor MainWindow partials
4. Improve error handling

### Phase 3: Performance (Weeks 6-7)
1. Optimize startup auto-refresh
2. Optimize TreeView operations
3. Profile memory usage
4. Implement caching

### Phase 4: Quality (Weeks 8-10)
1. Expand test coverage to 70%+
2. Implement structured logging
3. Improve accessibility
4. Complete documentation

---

## ?? Known Limitations

### WinUI3 TextBox Selection Background Bug
**Issue:** Selection highlight may persist after clicking away until text is scrolled  
**Status:** Framework bug, no known workaround  
**Workaround:** User can scroll slightly to clear the artifact  
**Tracked In:** `MainWindow.TextSearch.cs`, `DetailsViewService.cs`  
**Next Steps:** Consider filing bug with Microsoft WinUI3 team

---

## ?? Notes

- This document should be reviewed and updated quarterly
- Completed items should be dated and moved to `docs/CHANGELOG.md`
- New issues should be added with priority and estimated effort
- Consider creating GitHub issues for tracking individual tasks

---

**Last Review:** 2026-01-14  
**Next Review:** 2026-04-14
