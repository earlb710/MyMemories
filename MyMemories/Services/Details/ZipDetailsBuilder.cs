using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MyMemories.Services.Details;

/// <summary>
/// Builds ZIP file details content for the details panel.
/// </summary>
public class ZipDetailsBuilder
{
    private readonly StackPanel _detailsPanel;

    // Segoe MDL2 Assets glyphs
    private const string FileGlyph = "\uE8A5";
    private const string FolderGlyph = "\uE8B7";
    private const string SizeGlyph = "\uE7B8";
    private const string CalendarGlyph = "\uE787";
    private const string EditGlyph = "\uE70F";
    private const string ViewGlyph = "\uE7B3";
    private const string LockGlyph = "\uE72E";
    private const string ExtensionGlyph = "\uE8F9";

    public ZipDetailsBuilder(StackPanel detailsPanel)
    {
        _detailsPanel = detailsPanel;
    }

    /// <summary>
    /// Adds ZIP entry information to the details panel.
    /// </summary>
    public async Task AddZipEntryInfoAsync(LinkItem linkItem)
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
                Margin = new Thickness(0, 0, 0, 8),
                IsTextSelectionEnabled = true
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
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddZipEntryInfoAsync] Error: {ex.Message}");
            DetailsUIHelpers.AddWarning(_detailsPanel, $"Error displaying zip entry information: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds ZIP file information to the details panel.
    /// </summary>
    public void AddZipFileInfo(string path)
    {
        var fileInfo = new FileInfo(path);

        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "Zip Archive Information",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
            IsTextSelectionEnabled = true
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

    /// <summary>
    /// Adds manifest information to the details panel if the ZIP contains a manifest.
    /// </summary>
    public async Task AddManifestInfoAsync(string zipPath)
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

    /// <summary>
    /// Checks if a ZIP file contains a manifest.
    /// </summary>
    public async Task<bool> CheckZipHasManifestAsync(string zipFilePath)
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

    /// <summary>
    /// Gets the root category from the manifest in a ZIP file.
    /// </summary>
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

    private StackPanel CreateIconStatLine(string glyph, string text)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new FontIcon { Glyph = glyph, FontSize = 12 },
                new TextBlock { Text = text, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, IsTextSelectionEnabled = true }
            }
        };
    }

    private StackPanel CreateWarningLine(string text)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new FontIcon { Glyph = "\uE7BA", FontSize = 12, Foreground = new SolidColorBrush(Colors.Orange) },
                new TextBlock { Text = text, FontSize = 12, Foreground = new SolidColorBrush(Colors.Orange), IsTextSelectionEnabled = true }
            }
        };
    }

    private StackPanel CreateIconWarningLine(string glyph, string text)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new FontIcon { Glyph = glyph, FontSize = 12, Foreground = new SolidColorBrush(Colors.Orange) },
                new TextBlock { Text = text, FontSize = 12, Foreground = new SolidColorBrush(Colors.Orange), IsTextSelectionEnabled = true }
            }
        };
    }
}
