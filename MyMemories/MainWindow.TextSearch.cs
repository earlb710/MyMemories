using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MyMemories.Services;
using System;
using System.Text;

namespace MyMemories;

/// <summary>
/// Text search functionality and line number display for the content viewer.
/// </summary>
public sealed partial class MainWindow
{
    private TextSearchService _textSearchService = new();

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
        // TextBox has built-in scrolling via ScrollViewer attached properties
    }

    /// <summary>
    /// Synchronizes content scroll with line numbers scroll (Content Tab).
    /// </summary>
    private void ContentTabLineNumbersScroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        // TextBox has built-in scrolling via ScrollViewer attached properties
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
            ContentTabText.Focus(FocusState.Programmatic);
        }
        else
        {
            ContentSearchMatchCounter.Text = "No matches";
        }
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

