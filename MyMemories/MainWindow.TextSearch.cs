using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MyMemories.Services;
using System;
using System.Text;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Text search functionality and line number display for the content viewer.
/// </summary>
public sealed partial class MainWindow
{
    private TextSearchService _textSearchService = new();
    private ScrollViewer? _contentTextScrollViewer;
    private bool _isSyncingScroll = false;

    /// <summary>
    /// Sets up scroll synchronization between text content and line numbers.
    /// Call this after the TextBox is loaded.
    /// </summary>
    private void SetupScrollSynchronization()
    {
        System.Diagnostics.Debug.WriteLine("[SetupScrollSynchronization] ?????????????????????????");
        System.Diagnostics.Debug.WriteLine("[SetupScrollSynchronization] Starting setup...");
        
        // The TextBox might not be fully laid out yet, so we need to wait for it to load
        // Try to find the ScrollViewer first
        _contentTextScrollViewer = FindScrollViewer(ContentTabText);
        
        if (_contentTextScrollViewer == null)
        {
            System.Diagnostics.Debug.WriteLine($"[SetupScrollSynchronization] ScrollViewer not found yet, waiting for Loaded event...");
            
            // Wait for the TextBox to be fully loaded
            ContentTabText.Loaded += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[SetupScrollSynchronization] TextBox Loaded event fired, re-finding ScrollViewer...");
                _contentTextScrollViewer = FindScrollViewer(ContentTabText);
                
                if (_contentTextScrollViewer != null)
                {
                    // Remove old handler if it exists
                    _contentTextScrollViewer.ViewChanged -= OnContentTextScrollChanged;
                    // Add the event handler
                    _contentTextScrollViewer.ViewChanged += OnContentTextScrollChanged;
                    
                    // IMPORTANT: Sync initial scroll positions!
                    SyncInitialScrollPositions();
                    
                    System.Diagnostics.Debug.WriteLine($"[SetupScrollSynchronization] ? Text ScrollViewer found via Loaded event and hooked");
                    System.Diagnostics.Debug.WriteLine($"[SetupScrollSynchronization]   - ExtentHeight: {_contentTextScrollViewer.ExtentHeight:F0}");
                    System.Diagnostics.Debug.WriteLine($"[SetupScrollSynchronization]   - ViewportHeight: {_contentTextScrollViewer.ViewportHeight:F0}");
                    System.Diagnostics.Debug.WriteLine($"[SetupScrollSynchronization]   - VerticalOffset: {_contentTextScrollViewer.VerticalOffset:F0}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SetupScrollSynchronization] ? ERROR: Still could not find text ScrollViewer after Loaded!");
                }
            };
        }
        else
        {
            // Found it immediately - remove old event handler if it exists (prevent duplicate handlers)
            _contentTextScrollViewer.ViewChanged -= OnContentTextScrollChanged;
            // Add the event handler
            _contentTextScrollViewer.ViewChanged += OnContentTextScrollChanged;
            
            // IMPORTANT: Sync initial scroll positions!
            SyncInitialScrollPositions();
            
            System.Diagnostics.Debug.WriteLine($"[SetupScrollSynchronization] ? Text ScrollViewer found immediately and hooked");
            System.Diagnostics.Debug.WriteLine($"[SetupScrollSynchronization]   - ExtentHeight: {_contentTextScrollViewer.ExtentHeight:F0}");
            System.Diagnostics.Debug.WriteLine($"[SetupScrollSynchronization]   - ViewportHeight: {_contentTextScrollViewer.ViewportHeight:F0}");
            System.Diagnostics.Debug.WriteLine($"[SetupScrollSynchronization]   - VerticalOffset: {_contentTextScrollViewer.VerticalOffset:F0}");
        }
        
        if (ContentTabLineNumbersScroll != null)
        {
            // Remove old event handler if it exists (prevent duplicate handlers)
            ContentTabLineNumbersScroll.ViewChanged -= OnLineNumbersScrollChanged;
            // Add the event handler
            ContentTabLineNumbersScroll.ViewChanged += OnLineNumbersScrollChanged;
            
            System.Diagnostics.Debug.WriteLine($"[SetupScrollSynchronization] ? Line numbers ScrollViewer hooked");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[SetupScrollSynchronization] ? ERROR: Line numbers ScrollViewer is null!");
        }
        
        System.Diagnostics.Debug.WriteLine("[SetupScrollSynchronization] ?????????????????????????");
    }
    
    /// <summary>
    /// Synchronizes the initial scroll positions to ensure both start at zero.
    /// </summary>
    private void SyncInitialScrollPositions()
    {
        if (_contentTextScrollViewer != null && ContentTabLineNumbersScroll != null)
        {
            // Force both to scroll to zero - wait a bit for layout to complete
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                _isSyncingScroll = true;
                try
                {
                    _contentTextScrollViewer.ChangeView(null, 0, null, false);
                    ContentTabLineNumbersScroll.ChangeView(null, 0, null, false);
                    System.Diagnostics.Debug.WriteLine($"[SyncInitialScrollPositions] Reset both scrolls to zero");
                    System.Diagnostics.Debug.WriteLine($"[SyncInitialScrollPositions] Text offset: {_contentTextScrollViewer.VerticalOffset:F2}, LineNumbers offset: {ContentTabLineNumbersScroll.VerticalOffset:F2}");
                }
                finally
                {
                    Task.Delay(150).ContinueWith(_ =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            _isSyncingScroll = false;
                            // Verify they're actually at zero
                            if (_contentTextScrollViewer != null && ContentTabLineNumbersScroll != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[SyncInitialScrollPositions] After delay - Text: {_contentTextScrollViewer.VerticalOffset:F2}, LineNumbers: {ContentTabLineNumbersScroll.VerticalOffset:F2}");
                                
                                // If either is not at zero, force again
                                if (_contentTextScrollViewer.VerticalOffset > 0.1 || ContentTabLineNumbersScroll.VerticalOffset > 0.1)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[SyncInitialScrollPositions] Not aligned, forcing again...");
                                    _contentTextScrollViewer.ChangeView(null, 0, null, false);
                                    ContentTabLineNumbersScroll.ChangeView(null, 0, null, false);
                                }
                            }
                        });
                    });
                }
            });
        }
    }
    
    /// <summary>
    /// Handles scroll changes in the text content to sync line numbers.
    /// </summary>
    private void OnContentTextScrollChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_isSyncingScroll || ContentTabLineNumbersScroll == null)
            return;
        
        // Re-find the ScrollViewer if it's null (can happen after Hot Reload or content changes)
        if (_contentTextScrollViewer == null)
        {
            _contentTextScrollViewer = FindScrollViewer(ContentTabText);
            if (_contentTextScrollViewer == null)
                return;
        }
        
        _isSyncingScroll = true;
        try
        {
            var verticalOffset = _contentTextScrollViewer.VerticalOffset;
            
            // Safety check: if we're at the very top (offset ~0), force both to exact 0
            // Use larger threshold of 10 pixels to catch near-top scrolling
            if (verticalOffset < 10.0)
            {
                _contentTextScrollViewer.ChangeView(null, 0, null, false);
                ContentTabLineNumbersScroll.ChangeView(null, 0, null, false);
                System.Diagnostics.Debug.WriteLine($"[OnContentTextScrollChanged] Near top ({verticalOffset:F2}), forced both to 0");
            }
            else
            {
                // Use instant sync (no animation) for perfect tracking
                ContentTabLineNumbersScroll.ChangeView(null, verticalOffset, null, false);
            }
        }
        finally
        {
            // Shorter delay - only 10ms to prevent rapid feedback but not miss events
            Task.Delay(10).ContinueWith(_ =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _isSyncingScroll = false;
                });
            });
        }
    }
    
    /// <summary>
    /// Handles scroll changes in line numbers to sync text content.
    /// </summary>
    private void OnLineNumbersScrollChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_isSyncingScroll || ContentTabLineNumbersScroll == null)
            return;
        
        // Re-find the ScrollViewer if it's null
        if (_contentTextScrollViewer == null)
        {
            _contentTextScrollViewer = FindScrollViewer(ContentTabText);
            if (_contentTextScrollViewer == null)
                return;
        }
        
        _isSyncingScroll = true;
        try
        {
            var lineNumbersOffset = ContentTabLineNumbersScroll.VerticalOffset;
            
            // Safety check: if we're at the very top (offset ~0), force both to exact 0
            // Use larger threshold of 10 pixels to catch near-top scrolling
            if (lineNumbersOffset < 10.0)
            {
                _contentTextScrollViewer.ChangeView(null, 0, null, false);
                ContentTabLineNumbersScroll.ChangeView(null, 0, null, false);
                System.Diagnostics.Debug.WriteLine($"[OnLineNumbersScrollChanged] Near top ({lineNumbersOffset:F2}), forced both to 0");
            }
            else
            {
                // Use instant sync (no animation) for perfect tracking
                _contentTextScrollViewer.ChangeView(null, lineNumbersOffset, null, false);
            }
        }
        finally
        {
            // Shorter delay - only 10ms to prevent rapid feedback but not miss events
            Task.Delay(10).ContinueWith(_ =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _isSyncingScroll = false;
                });
            });
        }
    }

    /// <summary>
    /// Shows line numbers for the current text content (Content Tab).
    /// </summary>
    public void ShowLineNumbers(string content)
    {
        System.Diagnostics.Debug.WriteLine($"[ShowLineNumbers] Called with content length: {content?.Length ?? 0}");
        
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var maxLineNumber = lines.Length;
        var lineNumberWidth = maxLineNumber.ToString().Length;

        System.Diagnostics.Debug.WriteLine($"[ShowLineNumbers] Total lines: {lines.Length}, width: {lineNumberWidth}");

        var lineNumbers = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            lineNumbers.Append((i + 1).ToString().PadLeft(lineNumberWidth));
            if (i < lines.Length - 1)
                lineNumbers.AppendLine();
        }

        // Set line numbers in Content Tab
        ContentTabLineNumbersText.Text = lineNumbers.ToString();
        ContentTabLineNumbersBorder.Visibility = Visibility.Visible;
        
        System.Diagnostics.Debug.WriteLine($"[ShowLineNumbers] Line numbers set in Content Tab");
    }

    /// <summary>
    /// Hides line numbers (Content Tab).
    /// </summary>
    public void HideLineNumbers()
    {
        System.Diagnostics.Debug.WriteLine("[HideLineNumbers] Called");
        ContentTabLineNumbersBorder.Visibility = Visibility.Collapsed;
        ContentTabLineNumbersText.Text = string.Empty;
    }

    /// <summary>
    /// Synchronizes line numbers scroll with content scroll (Content Tab).
    /// </summary>
    private void ContentTabTextScroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        // When the text content scrolls, sync the line numbers
        var textScrollViewer = FindScrollViewer(ContentTabText);
        if (textScrollViewer != null && ContentTabLineNumbersScroll != null)
        {
            var verticalOffset = textScrollViewer.VerticalOffset;
            ContentTabLineNumbersScroll.ChangeView(null, verticalOffset, null, true);
        }
    }

    /// <summary>
    /// Synchronizes content scroll with line numbers scroll (Content Tab).
    /// </summary>
    private void ContentTabLineNumbersScroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        // When line numbers scroll, sync the text content
        var lineNumbersOffset = ContentTabLineNumbersScroll.VerticalOffset;
        var textScrollViewer = FindScrollViewer(ContentTabText);
        if (textScrollViewer != null)
        {
            textScrollViewer.ChangeView(null, lineNumbersOffset, null, true);
        }
    }

    /// <summary>
    /// Handles Ctrl+F keyboard shortcut at window level to open search panel.
    /// </summary>
    private void Window_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[Window_KeyDown] Key pressed: {e.Key}");
        
        // Check for Ctrl+F
        var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        bool isCtrlPressed = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

        System.Diagnostics.Debug.WriteLine($"[Window_KeyDown] Ctrl pressed: {isCtrlPressed}, Key is F: {e.Key == Windows.System.VirtualKey.F}");

        if (isCtrlPressed && e.Key == Windows.System.VirtualKey.F)
        {
            System.Diagnostics.Debug.WriteLine("[Window_KeyDown] Ctrl+F detected! Toggling search");
            
            // Toggle: if search panel is visible, close it; otherwise open it
            if (ContentTabSearchPanel?.Visibility == Visibility.Visible)
            {
                System.Diagnostics.Debug.WriteLine("[Window_KeyDown] Search panel is visible, closing it");
                CloseSearch_Click(sender, null!);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[Window_KeyDown] Search panel is hidden, opening it");
                ShowTextSearch();
            }
            
            e.Handled = true;
        }
    }

    /// <summary>
    /// Shows the text search panel.
    /// </summary>
    private void ShowTextSearch()
    {
        System.Diagnostics.Debug.WriteLine($"[ShowTextSearch] Called. ContentTabTextGrid visibility: {ContentTabTextGrid?.Visibility}");
        
        // Only show search if Content tab text viewer is visible
        if (ContentTabTextGrid?.Visibility != Visibility.Visible)
        {
            System.Diagnostics.Debug.WriteLine("[ShowTextSearch] ContentTabTextGrid not visible, aborting");
            return;
        }

        System.Diagnostics.Debug.WriteLine("[ShowTextSearch] Making search panel visible and focusing search box");
        ContentTabSearchPanel.Visibility = Visibility.Visible;
        ContentSearchTextBox.Focus(FocusState.Programmatic);

        // Select existing text if any
        if (!string.IsNullOrEmpty(ContentSearchTextBox.Text))
        {
            ContentSearchTextBox.SelectAll();
        }
    }

    /// <summary>
    /// Handles key presses in the search text box.
    /// </summary>
    private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            // Check if Shift is pressed
            var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
            bool isShiftPressed = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            if (isShiftPressed)
            {
                PerformSearch(searchForward: false);
            }
            else
            {
                PerformSearch(searchForward: true);
            }

            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CloseSearch_Click(sender, null!);
            e.Handled = true;
        }
        else
        {
            // Text changed - reset search on next search
            _textSearchService.Reset();
        }
    }

    /// <summary>
    /// Handles search next button click.
    /// </summary>
    private void SearchNext_Click(object sender, RoutedEventArgs e)
    {
        PerformSearch(searchForward: true);
    }

    /// <summary>
    /// Handles search previous button click.
    /// </summary>
    private void SearchPrevious_Click(object sender, RoutedEventArgs e)
    {
        PerformSearch(searchForward: false);
    }

    /// <summary>
    /// Handles case sensitive toggle click.
    /// </summary>
    private void SearchCaseSensitive_Click(object sender, RoutedEventArgs e)
    {
        // Reset search when case sensitivity changes
        _textSearchService.Reset();
    }

    /// <summary>
    /// Performs a text search in the current document.
    /// </summary>
    private void PerformSearch(bool searchForward)
    {
        var searchText = ContentSearchTextBox.Text;

        if (string.IsNullOrEmpty(searchText))
        {
            ContentSearchMatchCounter.Text = "0 of 0";
            return;
        }

        var content = ContentTabText.Text;
        var caseSensitive = ContentSearchCaseSensitiveToggle.IsChecked == true;

        // Start from beginning
        int startPosition = 0;

        MyMemories.Services.SearchResult result;
        
        if (_textSearchService._lastSearchText != searchText)
        {
            result = _textSearchService.FindFromPosition(content, searchText, startPosition, caseSensitive, searchForward);
        }
        else
        {
            result = _textSearchService.Search(content, searchText, caseSensitive, searchForward);
        }

        if (result.HasMatches)
        {
            ContentSearchMatchCounter.Text = $"{result.CurrentIndex} of {result.MatchCount}";
            
            // Calculate what line this match is on for debug output
            var textBeforeMatch = content.Substring(0, result.Position);
            var matchLine = textBeforeMatch.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length;
            
            System.Diagnostics.Debug.WriteLine($"[SEARCH] Match #{result.CurrentIndex} of {result.MatchCount} found at position {result.Position} (LINE {matchLine})");
            
            // **FIX: Select and scroll to the match**
            ContentTabText.Select(result.Position, result.Length);
            ContentTabText.Focus(FocusState.Programmatic);
            
            // Scroll to make the selection visible
            ScrollToSelection();
        }
        else
        {
            ContentSearchMatchCounter.Text = "No matches";
        }
    }

    /// <summary>
    /// Scrolls the text viewer to make the current selection visible.
    /// </summary>
    private void ScrollToSelection()
    {
        try
        {
            // The TextBox should automatically scroll the selection into view when we focus it
            // But we need to help it along by ensuring the ScrollViewer knows about the selection
            
            // First, make sure the TextBox has focus so it activates its scrolling
            ContentTabText.Focus(FocusState.Programmatic);
            
            // Give the UI thread a moment to process the focus and selection
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                // Get the ScrollViewer from the TextBox (re-find it to ensure we have the latest)
                var scrollViewer = FindScrollViewer(ContentTabText);
                
                // Update the cached reference for scroll synchronization
                if (scrollViewer != null && _contentTextScrollViewer != scrollViewer)
                {
                    // ScrollViewer changed, re-hook events
                    if (_contentTextScrollViewer != null)
                    {
                        _contentTextScrollViewer.ViewChanged -= OnContentTextScrollChanged;
                    }
                    _contentTextScrollViewer = scrollViewer;
                    _contentTextScrollViewer.ViewChanged += OnContentTextScrollChanged;
                    System.Diagnostics.Debug.WriteLine("[ScrollToSelection] Re-hooked scroll events");
                }
                
                if (scrollViewer != null)
                {
                    // Get current selection info
                    var selectionStart = ContentTabText.SelectionStart;
                    var text = ContentTabText.Text;
                    
                    if (selectionStart >= 0 && selectionStart <= text.Length)
                    {
                        // Count characters per line to find the target line
                        // Use proper line splitting to handle \r\n, \r, and \n
                        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        var currentCharCount = 0;
                        var targetLine = 0;
                        
                        // Calculate the actual line endings length for accurate counting
                        var lineEndingLength = text.Contains("\r\n") ? 2 : 1;
                        
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (currentCharCount + lines[i].Length >= selectionStart)
                            {
                                targetLine = i;
                                break;
                            }
                            currentCharCount += lines[i].Length + lineEndingLength;
                        }
                        
                        // Get scroll dimensions
                        var viewportHeight = scrollViewer.ViewportHeight;
                        var scrollableHeight = scrollViewer.ScrollableHeight;
                        var extentHeight = scrollViewer.ExtentHeight;
                        var totalLines = lines.Length;
                        
                        // Calculate the exact pixel position of the target line
                        // ExtentHeight is the TOTAL content height
                        // ScrollableHeight = ExtentHeight - ViewportHeight (the range we can scroll)
                        var lineHeightEstimate = extentHeight / Math.Max(totalLines, 1);
                        
                        // Target line's top pixel position in the content
                        var targetLinePixelTop = targetLine * lineHeightEstimate;
                        
                        // To center the line, we want it at viewport_height/2 from the top
                        // So scroll position should be: targetLinePixelTop - (viewportHeight / 2)
                        var desiredScrollOffset = targetLinePixelTop - (viewportHeight / 2);
                        
                        // Clamp to valid scroll range: [0, scrollableHeight]
                        // Note: scrollableHeight is already (extentHeight - viewportHeight)
                        var finalOffset = Math.Max(0, Math.Min(desiredScrollOffset, scrollableHeight));
                        
                        // Disable sync while we scroll programmatically to prevent feedback loop
                        _isSyncingScroll = true;
                        try
                        {
                            // Scroll to position
                            scrollViewer.ChangeView(null, finalOffset, null, false);
                            
                            // Synchronize line numbers
                            SyncLineNumbersScroll(finalOffset);
                            
                            // Calculate diagnostics
                            var visibleLines = viewportHeight / lineHeightEstimate;
                            // What line is actually at the top after this scroll
                            var actualTopLine = finalOffset / lineHeightEstimate;
                            var matchPositionInViewport = targetLine - actualTopLine;
                            
                            System.Diagnostics.Debug.WriteLine("???????????????????????????????????????????????????????");
                            System.Diagnostics.Debug.WriteLine($"[SEARCH] Found match on LINE {targetLine + 1} (of {totalLines} total lines)");
                            System.Diagnostics.Debug.WriteLine($"[SEARCH] Content: Extent={extentHeight:F0}px, Viewport={viewportHeight:F0}px, Scrollable={scrollableHeight:F0}px");
                            System.Diagnostics.Debug.WriteLine($"[SEARCH] Line height estimate: {lineHeightEstimate:F2}px, Visible lines: ~{visibleLines:F0}");
                            System.Diagnostics.Debug.WriteLine($"[SEARCH] Target line pixel top: {targetLinePixelTop:F0}px");
                            System.Diagnostics.Debug.WriteLine($"[SEARCH] Desired scroll (centered): {desiredScrollOffset:F0}px");
                            System.Diagnostics.Debug.WriteLine($"[SEARCH] Final scroll offset: {finalOffset:F0}px ({finalOffset / Math.Max(scrollableHeight, 1):P0} of scrollable)");
                            System.Diagnostics.Debug.WriteLine($"[SEARCH] Actual top line after scroll: ~{actualTopLine + 1:F0}");
                            System.Diagnostics.Debug.WriteLine($"[SEARCH] Match at viewport row: ~{matchPositionInViewport:F0} (should be ~{visibleLines / 2:F0} for centered)");
                            System.Diagnostics.Debug.WriteLine("???????????????????????????????????????????????????????");
                        }
                        finally
                        {
                            // Re-enable sync after a short delay to let scroll complete
                            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                            {
                                _isSyncingScroll = false;
                            });
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScrollToSelection] Error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Synchronizes the line numbers scroll position with the text content.
    /// </summary>
    private void SyncLineNumbersScroll(double verticalOffset)
    {
        try
        {
            if (ContentTabLineNumbersScroll != null)
            {
                ContentTabLineNumbersScroll.ChangeView(null, verticalOffset, null, false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SyncLineNumbersScroll] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Finds the ScrollViewer within a control's visual tree.
    /// </summary>
    private ScrollViewer? FindScrollViewer(DependencyObject element)
    {
        if (element is ScrollViewer scrollViewer)
            return scrollViewer;

        var childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < childCount; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(element, i);
            var result = FindScrollViewer(child);
            if (result != null)
                return result;
        }

        return null;
    }

    /// <summary>
    /// Closes the search panel.
    /// </summary>
    private void CloseSearch_Click(object sender, RoutedEventArgs e)
    {
        ContentTabSearchPanel.Visibility = Visibility.Collapsed;
        _textSearchService.Reset();
        ContentSearchTextBox.Text = string.Empty;
        ContentSearchMatchCounter.Text = "0 of 0";
        
        // Return focus to text viewer
        ContentTabText.Focus(FocusState.Programmatic);
    }
}

