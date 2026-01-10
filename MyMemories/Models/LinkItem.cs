using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;

namespace MyMemories;

/// <summary>
/// Represents a link item (file, directory, or URL) in the tree view.
/// </summary>
public class LinkItem : INotifyPropertyChanged
{
    private int _catalogFileCount;
    private ulong _catalogTotalSize;
    private DateTime? _lastCatalogUpdate;
    private bool _isZipPasswordProtected;
    private UrlStatus _urlStatus = UrlStatus.Unknown;
    private DateTime? _urlLastChecked;
    private string _urlStatusMessage = string.Empty;
    private string _description = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    
    public string Description 
    { 
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UrlStatusTooltip));
            }
        }
    }
    
    /// <summary>
    /// Keywords for searching and categorization. Comma or semicolon separated.
    /// </summary>
    public string Keywords { get; set; } = string.Empty;
    
    /// <summary>
    /// List of tag IDs assigned to this link.
    /// </summary>
    public List<string> TagIds { get; set; } = new();
    
    /// <summary>
    /// Gets formatted display text showing all tags with their names.
    /// Format: [tag icon] TagName  [tag icon] TagName2
    /// </summary>
    [JsonIgnore]
    public string TagDisplayText => Services.TagManagementService.Instance?.GetTagDisplayText(TagIds) ?? string.Empty;
    
    /// <summary>
    /// Gets whether this link has any tags assigned.
    /// </summary>
    [JsonIgnore]
    public Visibility HasTags => TagIds.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    
    /// <summary>
    /// Gets tag icons only for tree node display (no names).
    /// </summary>
    [JsonIgnore]
    public string TagIndicator => Services.TagManagementService.Instance?.GetTagIconsOnly(TagIds) ?? string.Empty;
    
    public bool IsDirectory { get; set; }
    public string CategoryPath { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    public FolderLinkType FolderType { get; set; } = FolderLinkType.LinkOnly;
    public string FileFilters { get; set; } = string.Empty;
    public bool IsCatalogEntry { get; set; }
    public SortOption CatalogSortOrder { get; set; } = SortOption.NameAscending;
    
    /// <summary>
    /// URL accessibility status (for bookmark categories).
    /// </summary>
    public UrlStatus UrlStatus
    {
        get => _urlStatus;
        set
        {
            if (_urlStatus != value)
            {
                _urlStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowUrlStatusBadge));
                OnPropertyChanged(nameof(UrlStatusColor));
            }
        }
    }

    /// <summary>
    /// Date and time when the URL status was last checked.
    /// </summary>
    public DateTime? UrlLastChecked
    {
        get => _urlLastChecked;
        set
        {
            if (_urlLastChecked != value)
            {
                _urlLastChecked = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Detailed message from the last URL status check.
    /// </summary>
    public string UrlStatusMessage
    {
        get => _urlStatusMessage;
        set
        {
            if (_urlStatusMessage != value)
            {
                _urlStatusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the visibility of the URL status badge.
    /// </summary>
    [JsonIgnore]
    public Visibility ShowUrlStatusBadge => UrlStatus != UrlStatus.Unknown ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Gets the color for the URL status badge.
    /// </summary>
    [JsonIgnore]
    public Windows.UI.Color UrlStatusColor
    {
        get
        {
            return UrlStatus switch
            {
                UrlStatus.Accessible => Microsoft.UI.Colors.LimeGreen,
                UrlStatus.Error => Microsoft.UI.Colors.Yellow,
                UrlStatus.NotFound => Microsoft.UI.Colors.Red,
                _ => Microsoft.UI.Colors.Gray
            };
        }
    }

    /// <summary>
    /// Gets the display text for URL status in tooltips.
    /// </summary>
    [JsonIgnore]
    public string UrlStatusDisplayText
    {
        get
        {
            return UrlStatus switch
            {
                UrlStatus.Accessible => "\u2705 URL is accessible", // ?
                UrlStatus.Error => "\u26A0 URL returned an error", // ?
                UrlStatus.NotFound => "\u274C URL not found", // ?
                UrlStatus.Unknown => "URL status not checked",
                _ => "URL status unknown"
            };
        }
    }

    /// <summary>
    /// Gets the full tooltip text for URL status.
    /// </summary>
    [JsonIgnore]
    public string UrlStatusTooltip
    {
        get
        {
            var baseTooltip = UrlStatus switch
            {
                UrlStatus.Accessible => "\u2705 URL is accessible\n\nClick to view the webpage", // ?
                UrlStatus.Error => "\u26A0 URL returned an error\n\nThe server responded with an error status.\nHover over the link item for details.", // ?
                UrlStatus.NotFound => "\u274C URL not found\n\nThe page does not exist or the server is unreachable.\nHover over the link item for details.", // ?
                UrlStatus.Unknown => "\u2753 URL status unknown\n\nClick 'Refresh URL State' on the category\nto check URL accessibility.", // ?
                _ => "URL status unknown"
            };

            if (UrlStatus != UrlStatus.Error && UrlStatus != UrlStatus.NotFound && !string.IsNullOrWhiteSpace(Description))
            {
                baseTooltip += $"\n\n\U0001F4DD {Description}"; // ??
            }

            return baseTooltip;
        }
    }

    /// <summary>
    /// Gets whether there is a URL status message to display.
    /// </summary>
    [JsonIgnore]
    public Visibility HasUrlStatusMessage => !string.IsNullOrWhiteSpace(UrlStatusMessage) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Gets whether the URL last checked date is available.
    /// </summary>
    [JsonIgnore]
    public Visibility HasUrlLastChecked => UrlLastChecked.HasValue ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Gets formatted text for when the URL was last checked.
    /// </summary>
    [JsonIgnore]
    public string UrlLastCheckedText => UrlLastChecked.HasValue 
        ? $"Last checked: {UrlLastChecked.Value:yyyy-MM-dd HH:mm:ss}" 
        : string.Empty;

    public DateTime? LastCatalogUpdate
    {
        get => _lastCatalogUpdate;
        set
        {
            if (_lastCatalogUpdate != value)
            {
                _lastCatalogUpdate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasChanged));
            }
        }
    }

    public ulong? FileSize { get; set; }
    public bool AutoRefreshCatalog { get; set; } = false;
    
    public bool IsZipPasswordProtected
    {
        get => _isZipPasswordProtected;
        set
        {
            if (_isZipPasswordProtected != value)
            {
                System.Diagnostics.Debug.WriteLine($"[LinkItem] IsZipPasswordProtected changing: {_isZipPasswordProtected} -> {value} for '{Title}'");
                _isZipPasswordProtected = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonIgnore]
    public int CatalogFileCount
    {
        get => _catalogFileCount;
        set
        {
            if (_catalogFileCount != value)
            {
                _catalogFileCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }

    [JsonIgnore]
    public ulong CatalogTotalSize
    {
        get => _catalogTotalSize;
        set
        {
            if (_catalogTotalSize != value)
            {
                _catalogTotalSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }

    /// <summary>
    /// Gets the display text for the tree node (without icon).
    /// </summary>
    [JsonIgnore]
    public string DisplayText
    {
        get
        {
            // Main folder with catalog
            if (IsDirectory && !IsCatalogEntry && CatalogFileCount > 0)
            {
                var sizeText = FormatFileSize(CatalogTotalSize);
                return $"{Title} ({CatalogFileCount} file{(CatalogFileCount != 1 ? "s" : "")}, {sizeText})";
            }
            
            // Catalog entry - subdirectory with file count
            if (IsDirectory && IsCatalogEntry && CatalogFileCount > 0)
            {
                var sizeText = FormatFileSize(CatalogTotalSize);
                return $"{Title} ({CatalogFileCount} file{(CatalogFileCount != 1 ? "s" : "")}, {sizeText})";
            }
            
            // Catalog entry - file with size
            if (!IsDirectory && IsCatalogEntry && FileSize.HasValue)
            {
                var sizeText = FormatFileSize(FileSize.Value);
                return $"{Title} ({sizeText})";
            }
            
            return Title;
        }
    }

    private static string FormatFileSize(ulong bytes)
    {
        return Utilities.FileUtilities.FormatFileSize(bytes);
    }

    /// <summary>
    /// Gets the visibility of the link badge for LinkOnly folders.
    /// </summary>
    [JsonIgnore]
    public Visibility ShowLinkBadge
    {
        get
        {
            if (IsDirectory && !IsCatalogEntry && FolderType == FolderLinkType.LinkOnly)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Gets the visibility of the change badge.
    /// </summary>
    [JsonIgnore]
    public Visibility HasChanged
    {
        get
        {
            if (!IsDirectory || !LastCatalogUpdate.HasValue)
                return Visibility.Collapsed;

            if (!Directory.Exists(Url))
                return Visibility.Collapsed;

            try
            {
                return HasDirectoryChangedRecursive(Url, LastCatalogUpdate.Value)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            catch
            {
                return Visibility.Collapsed;
            }
        }
    }

    private bool HasDirectoryChangedRecursive(string directoryPath, DateTime lastUpdate)
    {
        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);

            if (dirInfo.LastWriteTime > lastUpdate)
                return true;

            if (IsCatalogEntry)
            {
                var currentFileCount = Directory.GetFiles(directoryPath).Length;
                if (currentFileCount != CatalogFileCount)
                    return true;
            }

            var subdirectories = Directory.GetDirectories(directoryPath);
            foreach (var subdir in subdirectories)
            {
                var subdirInfo = new DirectoryInfo(subdir);
                if (subdirInfo.LastWriteTime > lastUpdate)
                    return true;

                if (HasDirectoryChangedRecursive(subdir, lastUpdate))
                    return true;
            }

            var files = Directory.GetFiles(directoryPath);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime > lastUpdate)
                    return true;
            }

            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void RefreshChangeStatus()
    {
        OnPropertyChanged(nameof(HasChanged));
    }

    [JsonIgnore]
    public string IconWithoutBadge => GetIconWithoutBadge();

    public string GetIconWithoutBadge()
    {
        if (!string.IsNullOrEmpty(Title) && Title.Length > 0)
        {
            char firstChar = Title[0];
            if (char.IsHighSurrogate(firstChar) || 
                firstChar == '\u2B50' || firstChar == '\u2764' || firstChar == '\u2705' || firstChar == '\u26A0' ||
                (firstChar >= 0x2600 && firstChar <= 0x26FF) ||
                (firstChar >= 0x2700 && firstChar <= 0x27BF) ||
                (firstChar >= 0x1F300 && firstChar <= 0x1F9FF))
            {
                return string.Empty;
            }
        }

        bool isZipArchive = IsDirectory && Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        if (IsDirectory)
        {
            if (isZipArchive)
                return "\U0001F4E6"; // ?? Package

            if (IsCatalogEntry)
                return "\U0001F4C1"; // ?? Folder

            return FolderType switch
            {
                FolderLinkType.LinkOnly => "\U0001F4C2", // ?? Open Folder
                FolderLinkType.CatalogueFiles => "\U0001F4C1", // ?? Folder
                FolderLinkType.FilteredCatalogue => "\U0001F50D", // ?? Magnifying Glass
                _ => "\U0001F4C1" // ?? Folder
            };
        }

        if (Uri.TryCreate(Url, UriKind.Absolute, out var uri) && !uri.IsFile)
            return "\U0001F310"; // ?? Globe

        var extension = Path.GetExtension(Url).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "\U0001F5BC", // ?? Framed Picture
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" => "\U0001F3AC", // ?? Clapper Board
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" => "\U0001F3B5", // ?? Musical Note
            ".pdf" => "\U0001F4D5", // ?? Closed Book (red, recognizable for PDF)
            ".doc" or ".docx" => "\U0001F4DD", // ?? Memo
            ".xls" or ".xlsx" => "\U0001F4CA", // ?? Bar Chart
            ".zip" or ".rar" or ".7z" => "\U0001F4E6", // ?? Package
            ".txt" or ".md" => "\U0001F4C4", // ?? Page Facing Up
            _ => "\U0001F4C4" // ?? Page Facing Up
        };
    }

    [JsonIgnore]
    public Visibility ShowZipBadge
    {
        get
        {
            if (IsDirectory && !IsCatalogEntry && Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }
    }

    public override string ToString()
    {
        var icon = GetIconWithoutBadge();

        // Main folder with catalog
        if (IsDirectory && !IsCatalogEntry && CatalogFileCount > 0)
        {
            var sizeText = FormatFileSize(CatalogTotalSize);
            return $"{icon} {Title} ({CatalogFileCount} file{(CatalogFileCount != 1 ? "s" : "")}, {sizeText})";
        }
        
        // Catalog entry - subdirectory with file count
        if (IsDirectory && IsCatalogEntry && CatalogFileCount > 0)
        {
            var sizeText = FormatFileSize(CatalogTotalSize);
            return $"{icon} {Title} ({CatalogFileCount} file{(CatalogFileCount != 1 ? "s" : "")}, {sizeText})";
        }
        
        // Catalog entry - file with size
        if (!IsDirectory && IsCatalogEntry && FileSize.HasValue)
        {
            var sizeText = FormatFileSize(FileSize.Value);
            return $"{icon} {Title} ({sizeText})";
        }

        return $"{icon} {Title}";
    }

    public string GetIcon()
    {
        return GetIconWithoutBadge();
    }

    public void NotifyTagsChanged()
    {
        OnPropertyChanged(nameof(TagIds));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
