using System;
using System.Collections.Generic;

namespace MyMemories.Services;

/// <summary>
/// Service for searching text within content with support for case sensitivity and direction.
/// </summary>
public class TextSearchService
{
    internal string _lastSearchText = string.Empty;
    private List<int> _matchPositions = new();
    private int _currentMatchIndex = -1;

    /// <summary>
    /// Searches for text in content and returns all match positions.
    /// </summary>
    public SearchResult Search(string content, string searchText, bool caseSensitive, bool searchForward = true)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(searchText))
        {
            return new SearchResult { MatchCount = 0, CurrentIndex = -1 };
        }

        // If search text changed, rebuild match list
        if (_lastSearchText != searchText || _matchPositions.Count == 0)
        {
            _lastSearchText = searchText;
            _matchPositions.Clear();
            _currentMatchIndex = -1;

            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            int index = 0;

            while ((index = content.IndexOf(searchText, index, comparison)) != -1)
            {
                _matchPositions.Add(index);
                index += searchText.Length;
            }
        }

        if (_matchPositions.Count == 0)
        {
            return new SearchResult { MatchCount = 0, CurrentIndex = -1 };
        }

        // Move to next/previous match
        if (searchForward)
        {
            _currentMatchIndex++;
            if (_currentMatchIndex >= _matchPositions.Count)
                _currentMatchIndex = 0;
        }
        else
        {
            _currentMatchIndex--;
            if (_currentMatchIndex < 0)
                _currentMatchIndex = _matchPositions.Count - 1;
        }

        return new SearchResult
        {
            MatchCount = _matchPositions.Count,
            CurrentIndex = _currentMatchIndex + 1,
            Position = _matchPositions[_currentMatchIndex],
            Length = searchText.Length
        };
    }

    /// <summary>
    /// Finds the first match starting from a specific position.
    /// </summary>
    public SearchResult FindFromPosition(string content, string searchText, int startPosition, bool caseSensitive, bool searchForward = true)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(searchText))
        {
            return new SearchResult { MatchCount = 0, CurrentIndex = -1 };
        }

        // Rebuild match list if needed
        if (_lastSearchText != searchText || _matchPositions.Count == 0)
        {
            _lastSearchText = searchText;
            _matchPositions.Clear();

            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            int index = 0;

            while ((index = content.IndexOf(searchText, index, comparison)) != -1)
            {
                _matchPositions.Add(index);
                index += searchText.Length;
            }
        }

        if (_matchPositions.Count == 0)
        {
            return new SearchResult { MatchCount = 0, CurrentIndex = -1 };
        }

        // Find the closest match from the start position
        if (searchForward)
        {
            // Find first match at or after startPosition
            _currentMatchIndex = _matchPositions.FindIndex(pos => pos >= startPosition);
            if (_currentMatchIndex == -1)
                _currentMatchIndex = 0; // Wrap around
        }
        else
        {
            // Find last match before startPosition
            _currentMatchIndex = _matchPositions.FindLastIndex(pos => pos < startPosition);
            if (_currentMatchIndex == -1)
                _currentMatchIndex = _matchPositions.Count - 1; // Wrap around
        }

        return new SearchResult
        {
            MatchCount = _matchPositions.Count,
            CurrentIndex = _currentMatchIndex + 1,
            Position = _matchPositions[_currentMatchIndex],
            Length = searchText.Length
        };
    }

    /// <summary>
    /// Resets the search state.
    /// </summary>
    public void Reset()
    {
        _lastSearchText = string.Empty;
        _matchPositions.Clear();
        _currentMatchIndex = -1;
    }
}

/// <summary>
/// Result of a text search operation.
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Total number of matches found.
    /// </summary>
    public int MatchCount { get; set; }

    /// <summary>
    /// Current match index (1-based).
    /// </summary>
    public int CurrentIndex { get; set; }

    /// <summary>
    /// Position of the current match in the text.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Length of the matched text.
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// Whether any matches were found.
    /// </summary>
    public bool HasMatches => MatchCount > 0;
}
