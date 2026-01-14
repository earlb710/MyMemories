using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;

namespace MyMemories.Services.Details;

/// <summary>
/// Builds link details content for the details panel.
/// </summary>
public class LinkDetailsBuilder
{
    private readonly StackPanel _detailsPanel;

    // Segoe MDL2 Assets glyphs
    private const string FileGlyph = "\uE8A5";        // Document
    private const string FolderGlyph = "\uE8B7";      // Folder
    private const string SizeGlyph = "\uE7B8";        // Package/Size
    private const string CalendarGlyph = "\uE787";    // Calendar
    private const string EditGlyph = "\uE70F";        // Edit/Modified
    private const string ViewGlyph = "\uE7B3";        // View/Accessed
    private const string WarningGlyph = "\uE7BA";     // Warning
    private const string LockGlyph = "\uE72E";        // Lock
    private const string ExtensionGlyph = "\uE8F9";   // Extension
    private const string ContainsGlyph = "\uE8B7";    // Contains

    public LinkDetailsBuilder(StackPanel detailsPanel)
    {
        _detailsPanel = detailsPanel;
    }

    /// <summary>
    /// Shows link details with file information and catalog buttons for directories.
    /// </summary>
    public async Task<(Button? createButton, Button? refreshButton)> ShowLinkDetailsAsync(
        LinkItem linkItem,
        TreeViewNode? node,
        Func<Task> onCreateCatalog,
        Func<Task> onRefreshCatalog,
        Func<Task>? onRefreshArchive = null,
        Func<Task>? onSaveCategory = null)
    {
        _detailsPanel.Children.Clear();

        Button? createButton = null;
        Button? refreshButton = null;

        // Add description at the top if available
        if (!string.IsNullOrWhiteSpace(linkItem.Description))
        {
            _detailsPanel.Children.Add(new TextBlock
            {
                Text = "Description",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });
            _detailsPanel.Children.Add(new TextBlock
            {
                Text = linkItem.Description,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(0, 0, 0, 16)
            });
        }

        bool isZipEntryUrl = !string.IsNullOrEmpty(linkItem.Url) && linkItem.Url.Contains("::");

        if (isZipEntryUrl)
        {
            await AddZipEntryInfoAsync(linkItem);
            return (null, null);
        }

        bool isZipFile = !string.IsNullOrEmpty(linkItem.Url) &&
                         linkItem.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                         File.Exists(linkItem.Url);

        bool isLinkOnlyFolder = linkItem.IsDirectory &&
                               linkItem.FolderType == FolderLinkType.LinkOnly &&
                               !linkItem.IsCatalogEntry; // Catalog entries are not "Link Only" folders

        bool directoryExists = linkItem.IsDirectory &&
                               !string.IsNullOrEmpty(linkItem.Url) &&
                               Directory.Exists(linkItem.Url);

        if (node != null && !isLinkOnlyFolder && (directoryExists || isZipFile))
        {
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 8)
            };

            bool hasCatalog = HasCatalogEntries(node);

            if (!hasCatalog)
            {
                createButton = CreateCatalogButton(isZipFile, onCreateCatalog);
                buttonPanel.Children.Add(createButton);
            }
            else
            {
                refreshButton = CreateRefreshButton(isZipFile, onRefreshCatalog);
                buttonPanel.Children.Add(refreshButton);

                if (isZipFile && onRefreshArchive != null)
                {
                    var hasManifest = await CheckZipHasManifestAsync(linkItem.Url);

                    if (hasManifest)
                    {
                        var refreshArchiveButton = CreateRefreshArchiveButton(onRefreshArchive);
                        buttonPanel.Children.Add(refreshArchiveButton);
                    }
                }
            }

            _detailsPanel.Children.Add(buttonPanel);

            if (hasCatalog && !isZipFile)
            {
                AddAutoRefreshCheckBox(linkItem, onSaveCategory);
            }

            if (isZipFile && hasCatalog)
            {
                await AddManifestInfoAsync(linkItem.Url);
            }
        }

        if (isLinkOnlyFolder)
        {
            AddLinkOnlyBanner();
        }

        if (node != null && (linkItem.IsDirectory || isZipFile) && HasCatalogEntries(node))
        {
            AddCatalogStatistics(node, isZipFile);
        }

        if (!string.IsNullOrWhiteSpace(linkItem.Url))
        {
            var pathLabel = isZipFile ? "Zip File Path" : (linkItem.IsDirectory ? "Directory Path" : "Path/URL");
            DetailsUIHelpers.AddSection(_detailsPanel, pathLabel, linkItem.Url, isSelectable: true);
        }
        else
        {
            DetailsUIHelpers.AddWarning(_detailsPanel, "No path or URL specified for this link");
        }

        // Add ratings display if ratings exist
        if (linkItem.Ratings.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[LinkDetailsBuilder] Link has {linkItem.Ratings.Count} ratings");
            System.Diagnostics.Debug.WriteLine($"[LinkDetailsBuilder] RatingManagementService.Instance = {RatingManagementService.Instance != null}");
            
            var ratingsPanel = RatingManagementService.Instance?.CreateRatingsDetailsPanel(linkItem.Ratings);
            
            System.Diagnostics.Debug.WriteLine($"[LinkDetailsBuilder] ratingsPanel = {ratingsPanel != null}, Children.Count = {ratingsPanel?.Children.Count ?? -1}");
            
            if (ratingsPanel != null && ratingsPanel.Children.Count > 0)
            {
                _detailsPanel.Children.Add(new TextBlock
                {
                    Text = "Ratings",
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                _detailsPanel.Children.Add(ratingsPanel);
                System.Diagnostics.Debug.WriteLine($"[LinkDetailsBuilder] Added ratings panel to details");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[LinkDetailsBuilder] Ratings panel was null or empty!");
                
                // Debug: Show raw ratings data
                foreach (var r in linkItem.Ratings)
                {
                    System.Diagnostics.Debug.WriteLine($"  Rating: '{r.Rating}', Score: {r.Score}, Reason: '{r.Reason}'");
                    var def = RatingManagementService.Instance?.GetDefinition(r.Rating);
                    System.Diagnostics.Debug.WriteLine($"  Definition found: {def != null} (Name: {def?.Name ?? "N/A"})");
                }
            }
        }

        AddTimestamps(linkItem);
        await AddFileSystemInfoAsync(linkItem, isZipFile);

        return (createButton, refreshButton);
    }

    private Button CreateCatalogButton(bool isZipFile, Func<Task> onCreateCatalog)
    {
        var button = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE8B7" },
                    new TextBlock { Text = "Create Catalog", VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };

        ToolTipService.SetToolTip(button, "Scans the " + (isZipFile ? "zip archive" : "directory") + " and creates a searchable catalog of all files and subdirectories");

        button.Click += async (s, e) =>
        {
            try { await onCreateCatalog(); }
            catch { }
        };

        return button;
    }

    private Button CreateRefreshButton(bool isZipFile, Func<Task> onRefreshCatalog)
    {
        var button = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE72C" },
                    new TextBlock { Text = "Refresh Catalog", VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };

        ToolTipService.SetToolTip(button, "Re-scans the " + (isZipFile ? "zip archive" : "directory") + " contents to update the catalog tree");

        button.Click += async (s, e) =>
        {
            try { await onRefreshCatalog(); }
            catch { }
        };

        return button;
    }

    private Button CreateRefreshArchiveButton(Func<Task> onRefreshArchive)
    {
        var button = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE777" },
                    new TextBlock { Text = "Refresh Archive", VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };

        ToolTipService.SetToolTip(button, "Re-creates the zip archive from the source category's current state (as specified in the manifest)");

        button.Click += async (s, e) =>
        {
            try { await onRefreshArchive(); }
            catch { }
        };

        return button;
    }

    private void AddAutoRefreshCheckBox(LinkItem linkItem, Func<Task>? onSaveCategory)
    {
        var autoRefreshCheckBox = new CheckBox
        {
            Content = "Auto-refresh catalog on startup",
            IsChecked = linkItem.AutoRefreshCatalog,
            Margin = new Thickness(0, 0, 0, 16)
        };

        autoRefreshCheckBox.Checked += async (s, e) =>
        {
            linkItem.AutoRefreshCatalog = true;
            if (onSaveCategory != null)
            {
                try { await onSaveCategory(); }
                catch { }
            }
        };

        autoRefreshCheckBox.Unchecked += async (s, e) =>
        {
            linkItem.AutoRefreshCatalog = false;
            if (onSaveCategory != null)
            {
                try { await onSaveCategory(); }
                catch { }
            }
        };

        _detailsPanel.Children.Add(autoRefreshCheckBox);
    }

    private async Task AddManifestInfoAsync(string zipPath)
    {
        var hasManifest = await CheckZipHasManifestAsync(zipPath);

        if (hasManifest)
        {
            var manifestRootCategory = await GetManifestRootCategoryAsync(zipPath);

            var manifestInfo = new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 0, 180, 0)),
                BorderBrush = new SolidColorBrush(Colors.Green),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 16),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new FontIcon
                        {
                            Glyph = "\uE8A5",
                            FontSize = 16,
                            Foreground = new SolidColorBrush(Colors.LightGreen)
                        },
                        new TextBlock
                        {
                            Text = $"This archive contains a manifest. Source category: {manifestRootCategory ?? "Unknown"}",
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Colors.White),
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            _detailsPanel.Children.Add(manifestInfo);
        }
    }

    private void AddLinkOnlyBanner()
    {
        var infoBanner = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 0, 120, 215)),
            BorderBrush = new SolidColorBrush(Colors.DodgerBlue),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 16),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon
                    {
                        Glyph = "\uE71B",
                        FontSize = 16,
                        Foreground = new SolidColorBrush(Colors.DodgerBlue)
                    },
                    new TextBlock
                    {
                        Text = "This is a Link Only folder. Use it to open the folder directly without cataloging its contents.",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Colors.White),
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
        _detailsPanel.Children.Add(infoBanner);
    }

    private void AddCatalogStatistics(TreeViewNode node, bool isZipFile)
    {
        var allCatalogEntries = node.Children
            .Where(child => child.Content is LinkItem link && link.IsCatalogEntry)
            .Select(child => child.Content as LinkItem)
            .Where(link => link != null)
            .ToList();

        if (allCatalogEntries.Count == 0)
            return;

        var fileEntries = allCatalogEntries.Where(link => !link!.IsDirectory).ToList();
        var directoryEntries = allCatalogEntries.Where(link => link!.IsDirectory).ToList();

        _detailsPanel.Children.Add(new TextBlock
        {
            Text = isZipFile ? "Zip Archive Contents" : "Catalog Statistics",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var statsPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
        statsPanel.Children.Add(CreateIconStatLine(FileGlyph, $"Total Files: {fileEntries.Count}"));

        if (directoryEntries.Count > 0)
        {
            statsPanel.Children.Add(CreateIconStatLine(FolderGlyph, $"Subdirectories: {directoryEntries.Count}"));
        }

        ulong totalSize = 0;
        int accessibleFiles = 0;

        foreach (var fileEntry in fileEntries)
        {
            try
            {
                if (isZipFile && fileEntry!.FileSize.HasValue)
                {
                    totalSize += fileEntry.FileSize.Value;
                    accessibleFiles++;
                }
                else if (File.Exists(fileEntry!.Url))
                {
                    var fileInfo = new FileInfo(fileEntry.Url);
                    totalSize += (ulong)fileInfo.Length;
                    accessibleFiles++;
                }
            }
            catch { }
        }

        if (accessibleFiles > 0)
        {
            statsPanel.Children.Add(CreateIconStatLine(SizeGlyph, $"Total Size: {FileViewerService.FormatFileSize(totalSize)}"));

            if (accessibleFiles < fileEntries.Count)
            {
                statsPanel.Children.Add(CreateWarningLine($"{fileEntries.Count - accessibleFiles} file(s) could not be accessed"));
            }
        }

        _detailsPanel.Children.Add(statsPanel);
    }

    private void AddTimestamps(LinkItem linkItem)
    {
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "Timestamps",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var timestampsPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
        timestampsPanel.Children.Add(CreateIconStatLine(CalendarGlyph, $"Created: {linkItem.CreatedDate:yyyy-MM-dd HH:mm:ss}"));
        timestampsPanel.Children.Add(CreateIconStatLine(EditGlyph, $"Modified: {linkItem.ModifiedDate:yyyy-MM-dd HH:mm:ss}"));

        _detailsPanel.Children.Add(timestampsPanel);
    }

    private async Task AddZipEntryInfoAsync(LinkItem linkItem)
    {
        try
        {
            var parts = linkItem.Url.Split(new[] { "::" }, 2, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                DetailsUIHelpers.AddWarning(_detailsPanel, "Invalid zip entry URL format");
                return;
            }

            var zipPath = parts[0];
            var entryPath = parts[1];

            string fileName;
            string extension;
            try
            {
                fileName = Path.GetFileName(entryPath.Replace('/', Path.DirectorySeparatorChar));
                extension = Path.GetExtension(entryPath);
            }
            catch
            {
                fileName = entryPath;
                extension = string.Empty;
            }

            _detailsPanel.Children.Add(new TextBlock
            {
                Text = "Zip Entry Information",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
            infoPanel.Children.Add(CreateIconStatLine(FileGlyph, $"File Name: {fileName}"));

            if (!string.IsNullOrEmpty(extension))
            {
                infoPanel.Children.Add(CreateIconStatLine(ExtensionGlyph, $"Extension: {extension}"));
            }

            infoPanel.Children.Add(CreateIconStatLine(FolderGlyph, $"Path in Archive: {entryPath}"));

            if (linkItem.FileSize.HasValue)
            {
                infoPanel.Children.Add(CreateIconStatLine(SizeGlyph, $"Size: {FileViewerService.FormatFileSize(linkItem.FileSize.Value)}"));
            }
            else
            {
                try
                {
                    if (File.Exists(zipPath))
                    {
                        var entryInfo = await Task.Run(() =>
                        {
                            try
                            {
                                using var archive = ZipFile.OpenRead(zipPath);
                                var normalizedPath = entryPath.Replace('\\', '/');
                                var entry = archive.GetEntry(normalizedPath) ?? archive.GetEntry(entryPath);
                                if (entry != null)
                                {
                                    return (found: true, size: (ulong)entry.Length, modified: entry.LastWriteTime.DateTime);
                                }
                            }
                            catch { }
                            return (found: false, size: 0UL, modified: DateTime.MinValue);
                        });

                        if (entryInfo.found)
                        {
                            infoPanel.Children.Add(CreateIconStatLine(SizeGlyph, $"Size: {FileViewerService.FormatFileSize(entryInfo.size)}"));
                            infoPanel.Children.Add(CreateIconStatLine(EditGlyph, $"Last Modified: {entryInfo.modified:yyyy-MM-dd HH:mm:ss}"));
                        }
                    }
                }
                catch { }
            }

            _detailsPanel.Children.Add(infoPanel);
            DetailsUIHelpers.AddSection(_detailsPanel, "Source Archive", zipPath, isSelectable: true);
            AddTimestamps(linkItem);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddZipEntryInfoAsync] Error: {ex.Message}");
            DetailsUIHelpers.AddWarning(_detailsPanel, $"Error displaying zip entry information: {ex.Message}");
        }
    }

    private async Task AddFileSystemInfoAsync(LinkItem linkItem, bool isZipFile)
    {
        try
        {
            if (isZipFile && File.Exists(linkItem.Url))
            {
                AddZipFileInfo(linkItem.Url);
            }
            else if (linkItem.IsDirectory && Directory.Exists(linkItem.Url))
            {
                await AddDirectoryInfoAsync(linkItem.Url);
            }
            else if (File.Exists(linkItem.Url))
            {
                AddFileInfo(linkItem.Url);
            }
        }
        catch (Exception ex)
        {
            DetailsUIHelpers.AddWarning(_detailsPanel, $"Unable to access file/directory information: {ex.Message}");
        }
    }

    private void AddZipFileInfo(string path)
    {
        var fileInfo = new FileInfo(path);

        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "Zip Archive Information",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };

        try
        {
            int fileCount = 0;
            int dirCount = 0;
            bool isPasswordProtected = false;

            try
            {
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, false))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                            dirCount++;
                        else if (!string.IsNullOrEmpty(entry.Name))
                            fileCount++;
                    }
                }
            }
            catch (InvalidDataException)
            {
                try
                {
                    using (var zipFile = new ICSharpCode.SharpZipLib.Zip.ZipFile(path))
                    {
                        isPasswordProtected = true;
                        foreach (ICSharpCode.SharpZipLib.Zip.ZipEntry entry in zipFile)
                        {
                            if (entry.IsDirectory)
                                dirCount++;
                            else
                                fileCount++;
                        }
                    }
                }
                catch { throw; }
            }

            if (isPasswordProtected)
            {
                infoPanel.Children.Add(CreateIconWarningLine(LockGlyph, "This archive is password-protected"));
            }

            infoPanel.Children.Add(CreateIconStatLine(FileGlyph, $"Files in Archive: {fileCount}"));
            if (dirCount > 0)
            {
                infoPanel.Children.Add(CreateIconStatLine(FolderGlyph, $"Folders in Archive: {dirCount}"));
            }
        }
        catch (Exception ex)
        {
            infoPanel.Children.Add(CreateWarningLine($"Could not read archive contents: {ex.Message}"));
        }

        infoPanel.Children.Add(CreateIconStatLine(SizeGlyph, $"Archive Size: {FileViewerService.FormatFileSize((ulong)fileInfo.Length)}"));
        infoPanel.Children.Add(CreateIconStatLine(ExtensionGlyph, $"Extension: {fileInfo.Extension}"));
        infoPanel.Children.Add(CreateIconStatLine(CalendarGlyph, $"Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}"));
        infoPanel.Children.Add(CreateIconStatLine(EditGlyph, $"Last Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}"));
        infoPanel.Children.Add(CreateIconStatLine(ViewGlyph, $"Last Accessed: {fileInfo.LastAccessTime:yyyy-MM-dd HH:mm:ss}"));

        _detailsPanel.Children.Add(infoPanel);
    }

    private async Task AddDirectoryInfoAsync(string path)
    {
        var dirInfo = new DirectoryInfo(path);

        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "Directory Information",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
        infoPanel.Children.Add(CreateIconStatLine(CalendarGlyph, $"Created: {dirInfo.CreationTime:yyyy-MM-dd HH:mm:ss}"));
        infoPanel.Children.Add(CreateIconStatLine(EditGlyph, $"Last Modified: {dirInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}"));
        infoPanel.Children.Add(CreateIconStatLine(ViewGlyph, $"Last Accessed: {dirInfo.LastAccessTime:yyyy-MM-dd HH:mm:ss}"));

        try
        {
            var files = dirInfo.GetFiles();
            var dirs = dirInfo.GetDirectories();
            infoPanel.Children.Add(CreateIconStatLine(ContainsGlyph, $"Contains: {files.Length} file(s), {dirs.Length} folder(s)"));
        }
        catch { }

        _detailsPanel.Children.Add(infoPanel);
    }

    private void AddFileInfo(string path)
    {
        var fileInfo = new FileInfo(path);

        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "File Information",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
        infoPanel.Children.Add(CreateIconStatLine(SizeGlyph, $"Size: {FileViewerService.FormatFileSize((ulong)fileInfo.Length)}"));
        infoPanel.Children.Add(CreateIconStatLine(ExtensionGlyph, $"Extension: {fileInfo.Extension}"));
        infoPanel.Children.Add(CreateIconStatLine(CalendarGlyph, $"Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}"));
        infoPanel.Children.Add(CreateIconStatLine(EditGlyph, $"Last Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}"));
        infoPanel.Children.Add(CreateIconStatLine(ViewGlyph, $"Last Accessed: {fileInfo.LastAccessTime:yyyy-MM-dd HH:mm:ss}"));

        _detailsPanel.Children.Add(infoPanel);
    }

    /// <summary>
    /// Creates a stat line with an icon and text.
    /// </summary>
    private StackPanel CreateIconStatLine(string glyph, string text)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new FontIcon { Glyph = glyph, FontSize = 12 },
                new TextBlock { Text = text, FontSize = 14, VerticalAlignment = VerticalAlignment.Center }
            }
        };
    }

    /// <summary>
    /// Creates a warning line with an icon and text.
    /// </summary>
    private StackPanel CreateWarningLine(string text)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new FontIcon { Glyph = WarningGlyph, FontSize = 12, Foreground = new SolidColorBrush(Colors.Orange) },
                new TextBlock { Text = text, FontSize = 12, Foreground = new SolidColorBrush(Colors.Orange), TextWrapping = TextWrapping.Wrap }
            }
        };
    }

    /// <summary>
    /// Creates a warning line with a specific icon and text.
    /// </summary>
    private StackPanel CreateIconWarningLine(string glyph, string text)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 8),
            Children =
            {
                new FontIcon { Glyph = glyph, FontSize = 14, Foreground = new SolidColorBrush(Colors.Orange) },
                new TextBlock 
                { 
                    Text = text, 
                    FontSize = 14, 
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Colors.Orange) 
                }
            }
        };
    }

    private bool HasCatalogEntries(TreeViewNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link && link.IsCatalogEntry)
                return true;
        }
        return false;
    }

    private async Task<bool> CheckZipHasManifestAsync(string zipFilePath)
    {
        if (!File.Exists(zipFilePath))
            return false;

        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var archive = ZipFile.OpenRead(zipFilePath))
                    {
                        return archive.GetEntry("_MANIFEST.txt") != null;
                    }
                }
                catch (InvalidDataException)
                {
                    try
                    {
                        using (var zipFile = new ICSharpCode.SharpZipLib.Zip.ZipFile(zipFilePath))
                        {
                            return zipFile.GetEntry("_MANIFEST.txt") != null;
                        }
                    }
                    catch { return false; }
                }
                catch { return false; }
            });
        }
        catch { return false; }
    }

    private async Task<string?> GetManifestRootCategoryAsync(string zipFilePath)
    {
        if (!File.Exists(zipFilePath))
            return null;

        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var archive = ZipFile.OpenRead(zipFilePath))
                    {
                        var manifestEntry = archive.GetEntry("_MANIFEST.txt");
                        if (manifestEntry == null)
                            return null;

                        using (var stream = manifestEntry.Open())
                        using (var reader = new StreamReader(stream))
                        {
                            var content = reader.ReadToEnd();
                            var match = Regex.Match(content, @"Root Category:\s*(.+)", RegexOptions.Multiline);
                            if (match.Success)
                            {
                                return match.Groups[1].Value.Trim();
                            }
                            return null;
                        }
                    }
                }
                catch (InvalidDataException)
                {
                    return "Password Protected";
                }
                catch { return null; }
            });
        }
        catch { return null; }
    }

    /// <summary>
    /// Adds an "Open in Explorer" button for directories.
    /// </summary>
    public async Task<Button?> AddOpenInExplorerButtonAsync(string path)
    {
        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(path);
            var openButton = new Button
            {
                Content = "Open in File Explorer",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 16, 0, 0)
            };

            openButton.Click += async (s, e) =>
            {
                try { await Windows.System.Launcher.LaunchFolderAsync(folder); }
                catch { }
            };

            _detailsPanel.Children.Add(openButton);
            return openButton;
        }
        catch { return null; }
    }
}
