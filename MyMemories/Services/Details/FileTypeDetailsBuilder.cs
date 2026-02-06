using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.Threading.Tasks;
using MyMemories.Services;

namespace MyMemories.Services.Details;

/// <summary>
/// Builds file type-specific metadata details (images, PDFs) for the details panel.
/// </summary>
public class FileTypeDetailsBuilder
{
    private readonly StackPanel _detailsPanel;

    // Segoe MDL2 Assets glyphs
    private const string SizeGlyph = "\uE7B8";
    private const string CalendarGlyph = "\uE787";
    private const string EditGlyph = "\uE70F";
    private const string ViewGlyph = "\uE7B3";
    private const string LockGlyph = "\uE72E";
    private const string ExtensionGlyph = "\uE8F9";

    public FileTypeDetailsBuilder(StackPanel detailsPanel)
    {
        _detailsPanel = detailsPanel;
    }

    /// <summary>
    /// Adds detailed information panel for image files with EXIF metadata.
    /// </summary>
    public async Task AddImageFileInfoAsync(string path, FileInfo fileInfo)
    {
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "Image Information",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
            IsTextSelectionEnabled = true
        });

        var infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
        
        // Basic file info
        infoPanel.Children.Add(CreateIconStatLine(SizeGlyph, $"File Size: {FileViewerService.FormatFileSize((ulong)fileInfo.Length)}"));
        infoPanel.Children.Add(CreateIconStatLine(ExtensionGlyph, $"Format: {fileInfo.Extension.ToUpperInvariant().TrimStart('.')}"));

        // Try to extract image metadata
        var metadata = await ImageMetadataService.ExtractMetadataAsync(path);
        
        if (metadata != null)
        {
            // Dimensions & Technical Info
            if (metadata.PixelWidth > 0 && metadata.PixelHeight > 0)
            {
                infoPanel.Children.Add(CreateIconStatLine("\uE91B", $"Dimensions: {metadata.PixelWidth} × {metadata.PixelHeight} pixels"));
                infoPanel.Children.Add(CreateIconStatLine("\uE7C5", $"Megapixels: {metadata.Megapixels}"));
                infoPanel.Children.Add(CreateIconStatLine("\uE7C5", $"Aspect Ratio: {metadata.AspectRatio}"));
            }

            if (metadata.DpiX > 0 && metadata.DpiY > 0)
            {
                var dpiText = metadata.DpiX == metadata.DpiY 
                    ? $"Resolution: {metadata.DpiX:F0} DPI" 
                    : $"Resolution: {metadata.DpiX:F0} × {metadata.DpiY:F0} DPI";
                infoPanel.Children.Add(CreateIconStatLine("\uE7C5", dpiText));
            }

            // Camera Information
            if (!string.IsNullOrEmpty(metadata.CameraManufacturer) || !string.IsNullOrEmpty(metadata.CameraModel))
            {
                _detailsPanel.Children.Add(infoPanel);
                infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 16) };
                
                _detailsPanel.Children.Add(new TextBlock
                {
                    Text = "Camera Information",
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 8),
                    IsTextSelectionEnabled = true
                });

                if (!string.IsNullOrEmpty(metadata.CameraManufacturer))
                    infoPanel.Children.Add(CreateIconStatLine("\uE7F4", $"Manufacturer: {metadata.CameraManufacturer}"));
                
                if (!string.IsNullOrEmpty(metadata.CameraModel))
                    infoPanel.Children.Add(CreateIconStatLine("\uE960", $"Model: {metadata.CameraModel}"));
            }

            // Camera Settings (EXIF)
            if (metadata.IsoSpeed.HasValue || !string.IsNullOrEmpty(metadata.ExposureTime) || 
                !string.IsNullOrEmpty(metadata.FNumber) || !string.IsNullOrEmpty(metadata.FocalLength))
            {
                if (infoPanel.Children.Count > 0)
                {
                    _detailsPanel.Children.Add(infoPanel);
                    infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 16) };
                }

                _detailsPanel.Children.Add(new TextBlock
                {
                    Text = "Camera Settings",
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 8),
                    IsTextSelectionEnabled = true
                });

                if (metadata.IsoSpeed.HasValue)
                    infoPanel.Children.Add(CreateIconStatLine("\uE7C5", $"ISO: {metadata.IsoSpeed.Value}"));
                
                if (!string.IsNullOrEmpty(metadata.ExposureTime))
                    infoPanel.Children.Add(CreateIconStatLine("\uE916", $"Shutter Speed: {metadata.ExposureTime}"));
                
                if (!string.IsNullOrEmpty(metadata.FNumber))
                    infoPanel.Children.Add(CreateIconStatLine("\uE7C5", $"Aperture: {metadata.FNumber}"));
                
                if (!string.IsNullOrEmpty(metadata.FocalLength))
                    infoPanel.Children.Add(CreateIconStatLine("\uE714", $"Focal Length: {metadata.FocalLength}"));
                
                if (!string.IsNullOrEmpty(metadata.Flash))
                    infoPanel.Children.Add(CreateIconStatLine("\uE793", $"Flash: {metadata.Flash}"));
            }

            // GPS Location
            if (!string.IsNullOrEmpty(metadata.GpsLocation))
            {
                if (infoPanel.Children.Count > 0)
                {
                    _detailsPanel.Children.Add(infoPanel);
                    infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 16) };
                }

                _detailsPanel.Children.Add(new TextBlock
                {
                    Text = "Location",
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 8),
                    IsTextSelectionEnabled = true
                });

                infoPanel.Children.Add(CreateIconStatLine("\uE707", $"GPS: {metadata.GpsLocation}"));
            }

            // Author & Copyright
            if (!string.IsNullOrEmpty(metadata.Artist) || !string.IsNullOrEmpty(metadata.Copyright) || 
                !string.IsNullOrEmpty(metadata.Software) || !string.IsNullOrEmpty(metadata.ImageDescription))
            {
                if (infoPanel.Children.Count > 0)
                {
                    _detailsPanel.Children.Add(infoPanel);
                    infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 16) };
                }

                _detailsPanel.Children.Add(new TextBlock
                {
                    Text = "Author & Details",
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 8),
                    IsTextSelectionEnabled = true
                });

                if (!string.IsNullOrEmpty(metadata.Artist))
                    infoPanel.Children.Add(CreateIconStatLine("\uE77B", $"Artist: {metadata.Artist}"));
                
                if (!string.IsNullOrEmpty(metadata.Copyright))
                    infoPanel.Children.Add(CreateIconStatLine("\uE72E", $"Copyright: {metadata.Copyright}"));
                
                if (!string.IsNullOrEmpty(metadata.Software))
                    infoPanel.Children.Add(CreateIconStatLine("\uE90F", $"Software: {metadata.Software}"));
                
                if (!string.IsNullOrEmpty(metadata.ImageDescription))
                    infoPanel.Children.Add(CreateIconStatLine("\uE8C8", $"Description: {metadata.ImageDescription}"));
            }

            // Dates
            if (infoPanel.Children.Count > 0)
            {
                _detailsPanel.Children.Add(infoPanel);
                infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 16) };
            }

            _detailsPanel.Children.Add(new TextBlock
            {
                Text = "Timestamps",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 8)
            });

            if (metadata.DateTaken.HasValue)
                infoPanel.Children.Add(CreateIconStatLine("\uE787", $"Photo Taken: {metadata.DateTaken.Value:yyyy-MM-dd HH:mm:ss}"));
            
            infoPanel.Children.Add(CreateIconStatLine(CalendarGlyph, $"File Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}"));
            infoPanel.Children.Add(CreateIconStatLine(EditGlyph, $"File Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}"));
        }
        else
        {
            // Fallback if metadata extraction fails - show basic file info
            infoPanel.Children.Add(CreateIconStatLine(CalendarGlyph, $"Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}"));
            infoPanel.Children.Add(CreateIconStatLine(EditGlyph, $"Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}"));
            infoPanel.Children.Add(CreateIconStatLine(ViewGlyph, $"Accessed: {fileInfo.LastAccessTime:yyyy-MM-dd HH:mm:ss}"));
        }

        _detailsPanel.Children.Add(infoPanel);
    }

    /// <summary>
    /// Adds detailed information panel for PDF files with document metadata.
    /// </summary>
    public async Task AddPdfFileInfoAsync(string path, FileInfo fileInfo)
    {
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "PDF Document Information",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
        
        // Basic file info
        infoPanel.Children.Add(CreateIconStatLine(SizeGlyph, $"File Size: {FileViewerService.FormatFileSize((ulong)fileInfo.Length)}"));

        // Try to extract PDF metadata
        var metadata = await PdfMetadataService.ExtractMetadataAsync(path);
        
        if (metadata != null)
        {
            // Page Count & Version
            infoPanel.Children.Add(CreateIconStatLine("\uE8A4", $"Pages: {metadata.PageCount}"));
            
            if (!string.IsNullOrEmpty(metadata.PdfVersion))
                infoPanel.Children.Add(CreateIconStatLine("\uE8E5", $"PDF Version: {metadata.PdfVersion}"));
            
            if (metadata.IsPasswordProtected)
                infoPanel.Children.Add(CreateIconStatLine(LockGlyph, "Password Protected: Yes"));
            
            // Page Dimensions
            if (metadata.PageWidth > 0 && metadata.PageHeight > 0)
            {
                _detailsPanel.Children.Add(infoPanel);
                infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 16) };
                
                _detailsPanel.Children.Add(new TextBlock
                {
                    Text = "Page Layout",
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 8)
                });

                if (!string.IsNullOrEmpty(metadata.PageSizeDescription))
                    infoPanel.Children.Add(CreateIconStatLine("\uE8A1", $"Page Size: {metadata.PageSizeDescription}"));
                else
                    infoPanel.Children.Add(CreateIconStatLine("\uE8A1", $"Page Size: {metadata.PageDimensions}"));
                
                if (!string.IsNullOrEmpty(metadata.PageOrientation))
                    infoPanel.Children.Add(CreateIconStatLine("\uE7C5", $"Orientation: {metadata.PageOrientation}"));
            }

            // Document Title & Subject
            if (!string.IsNullOrEmpty(metadata.Title) || !string.IsNullOrEmpty(metadata.Subject))
            {
                _detailsPanel.Children.Add(infoPanel);
                infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 16) };
                
                _detailsPanel.Children.Add(new TextBlock
                {
                    Text = "Document Details",
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 8)
                });

                if (!string.IsNullOrEmpty(metadata.Title))
                    infoPanel.Children.Add(CreateIconStatLine("\uE8C8", $"Title: {metadata.Title}"));
                
                if (!string.IsNullOrEmpty(metadata.Subject))
                    infoPanel.Children.Add(CreateIconStatLine("\uE8C8", $"Subject: {metadata.Subject}"));
                
                if (!string.IsNullOrEmpty(metadata.Keywords))
                    infoPanel.Children.Add(CreateIconStatLine("\uE8EC", $"Keywords: {metadata.Keywords}"));
            }

            // Author Information
            if (!string.IsNullOrEmpty(metadata.Author) || !string.IsNullOrEmpty(metadata.Creator) || 
                !string.IsNullOrEmpty(metadata.Producer))
            {
                _detailsPanel.Children.Add(infoPanel);
                infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 16) };
                
                _detailsPanel.Children.Add(new TextBlock
                {
                    Text = "Author & Creation",
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 8),
                    IsTextSelectionEnabled = true
                });

                if (!string.IsNullOrEmpty(metadata.Author))
                    infoPanel.Children.Add(CreateIconStatLine("\uE77B", $"Author: {metadata.Author}"));
                
                if (!string.IsNullOrEmpty(metadata.Creator))
                    infoPanel.Children.Add(CreateIconStatLine("\uE90F", $"Creator: {metadata.Creator}"));
                
                if (!string.IsNullOrEmpty(metadata.Producer))
                    infoPanel.Children.Add(CreateIconStatLine("\uE90F", $"Producer: {metadata.Producer}"));
            }

            // PDF Timestamps
            _detailsPanel.Children.Add(infoPanel);
            infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 16) };
            
            _detailsPanel.Children.Add(new TextBlock
            {
                Text = "Timestamps",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 8),
                IsTextSelectionEnabled = true
            });

            if (metadata.PdfCreationDate.HasValue)
                infoPanel.Children.Add(CreateIconStatLine("\uE787", $"PDF Created: {metadata.PdfCreationDate.Value:yyyy-MM-dd HH:mm:ss}"));
            
            if (metadata.PdfModificationDate.HasValue)
                infoPanel.Children.Add(CreateIconStatLine(EditGlyph, $"PDF Modified: {metadata.PdfModificationDate.Value:yyyy-MM-dd HH:mm:ss}"));
            
            infoPanel.Children.Add(CreateIconStatLine(CalendarGlyph, $"File Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}"));
            infoPanel.Children.Add(CreateIconStatLine(EditGlyph, $"File Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}"));
        }
        else
        {
            // Fallback if metadata extraction fails - show basic file info
            infoPanel.Children.Add(CreateIconStatLine(ExtensionGlyph, $"Format: PDF"));
            infoPanel.Children.Add(CreateIconStatLine(CalendarGlyph, $"Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}"));
            infoPanel.Children.Add(CreateIconStatLine(EditGlyph, $"Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}"));
            infoPanel.Children.Add(CreateIconStatLine(ViewGlyph, $"Accessed: {fileInfo.LastAccessTime:yyyy-MM-dd HH:mm:ss}"));
        }

        _detailsPanel.Children.Add(infoPanel);
        
        // Add Data Extractor button
        var extractButton = new Button
        {
            Content = "📊 Data Extractor",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 16, 0, 0)
        };
        extractButton.Click += (s, e) =>
        {
            try
            {
                var extractorWindow = new DataExtractorWindow(path);
                extractorWindow.Activate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileTypeDetailsBuilder] Error opening Data Extractor: {ex.Message}");
            }
        };
        _detailsPanel.Children.Add(extractButton);
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
}
