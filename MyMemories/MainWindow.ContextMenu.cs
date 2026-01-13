using Microsoft.UI.Xaml;

namespace MyMemories;

/// <summary>
/// Context menu entry point.
/// Event handlers are now organized in separate files:
/// - MainWindow.ContextMenu.Configuration.cs: Menu configuration and population
/// - MainWindow.ContextMenu.Category.cs: Category menu event handlers
/// - MainWindow.ContextMenu.Link.cs: Link menu event handlers (basic operations)
/// - MainWindow.ContextMenu.Summarize.cs: Link menu URL summarize feature
/// - MainWindow.ContextMenu.Shared.cs: Shared helper methods (sort dialog)
/// - MainWindow.ContextMenu.Helpers.cs: Helper utilities and caching
/// </summary>
public sealed partial class MainWindow
{
    // All context menu implementation has been split into focused files for better maintainability:
    // 
    // Configuration & Setup:
    //   - MainWindow.ContextMenu.Configuration.cs (right-click handling, menu configuration)
    //   - MainWindow.ContextMenu.Helpers.cs (caching, lookups, utilities)
    //
    // Event Handlers:
    //   - MainWindow.ContextMenu.Category.cs (all category menu handlers)
    //   - MainWindow.ContextMenu.Link.cs (all link menu handlers)
    //   - MainWindow.ContextMenu.Summarize.cs (URL summarize feature)
    //
    // Shared Methods:
    //   - MainWindow.ContextMenu.Shared.cs (ShowSortDialogAsync)
}
