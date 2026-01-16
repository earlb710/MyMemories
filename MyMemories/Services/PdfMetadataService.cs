using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage;

namespace MyMemories.Services;

/// <summary>
/// Service for extracting metadata from PDF files.
/// Extracts page count, document properties, and PDF-specific information.
/// </summary>
public static class PdfMetadataService
{
    /// <summary>
    /// Extracts comprehensive metadata from a PDF file.
    /// </summary>
    public static async Task<PdfMetadata?> ExtractMetadataAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(filePath);
            var metadata = new PdfMetadata();

            // Get basic file properties
            var fileInfo = new FileInfo(filePath);
            metadata.FileSize = (ulong)fileInfo.Length;
            metadata.DateModified = fileInfo.LastWriteTime;
            metadata.DateCreated = fileInfo.CreationTime;

            // Load PDF document
            var pdfDocument = await PdfDocument.LoadFromFileAsync(file);
            
            if (pdfDocument != null)
            {
                // Page information
                metadata.PageCount = (int)pdfDocument.PageCount;
                metadata.IsPasswordProtected = pdfDocument.IsPasswordProtected;
                
                // Get first page dimensions (representative of document)
                if (pdfDocument.PageCount > 0)
                {
                    using var page = pdfDocument.GetPage(0);
                    metadata.PageWidth = page.Size.Width;
                    metadata.PageHeight = page.Size.Height;
                    metadata.PageOrientation = page.Size.Width > page.Size.Height ? "Landscape" : "Portrait";
                    
                    // Calculate page size in common formats
                    metadata.PageSizeDescription = GetPageSizeDescription(page.Size.Width, page.Size.Height);
                }
            }

            // Try to read PDF properties from file stream
            await ExtractPdfPropertiesAsync(filePath, metadata);

            return metadata;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PdfMetadataService] Error extracting metadata: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extracts PDF document properties by reading the file header.
    /// </summary>
    private static async Task ExtractPdfPropertiesAsync(string filePath, PdfMetadata metadata)
    {
        try
        {
            // Read first few KB to get PDF version and basic info
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(fileStream);
            
            // Read header to get PDF version
            var header = new char[100];
            await reader.ReadAsync(header, 0, 100);
            var headerText = new string(header);
            
            // PDF version is in format: %PDF-1.x
            if (headerText.StartsWith("%PDF-"))
            {
                var versionEnd = headerText.IndexOf('\r');
                if (versionEnd == -1) versionEnd = headerText.IndexOf('\n');
                if (versionEnd == -1) versionEnd = 8;
                
                var version = headerText.Substring(5, Math.Min(3, versionEnd - 5)).Trim();
                metadata.PdfVersion = version;
            }
            
            // Read more content to find metadata
            fileStream.Position = 0;
            var buffer = new byte[Math.Min(65536, fileStream.Length)]; // First 64KB
            await fileStream.ReadAsync(buffer, 0, buffer.Length);
            var content = System.Text.Encoding.Latin1.GetString(buffer);
            
            // Extract common PDF metadata
            metadata.Title = ExtractPdfProperty(content, "/Title");
            metadata.Author = ExtractPdfProperty(content, "/Author");
            metadata.Subject = ExtractPdfProperty(content, "/Subject");
            metadata.Keywords = ExtractPdfProperty(content, "/Keywords");
            metadata.Creator = ExtractPdfProperty(content, "/Creator");
            metadata.Producer = ExtractPdfProperty(content, "/Producer");
            
            // Extract dates
            var creationDate = ExtractPdfProperty(content, "/CreationDate");
            if (!string.IsNullOrEmpty(creationDate))
            {
                metadata.PdfCreationDate = ParsePdfDate(creationDate);
            }
            
            var modDate = ExtractPdfProperty(content, "/ModDate");
            if (!string.IsNullOrEmpty(modDate))
            {
                metadata.PdfModificationDate = ParsePdfDate(modDate);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PdfMetadataService] Error reading PDF properties: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts a property value from PDF content.
    /// </summary>
    private static string? ExtractPdfProperty(string content, string propertyName)
    {
        try
        {
            var index = content.IndexOf(propertyName, StringComparison.OrdinalIgnoreCase);
            if (index == -1) return null;
            
            var startIndex = index + propertyName.Length;
            
            // Skip whitespace
            while (startIndex < content.Length && (content[startIndex] == ' ' || content[startIndex] == '\t'))
                startIndex++;
            
            if (startIndex >= content.Length) return null;
            
            // Check for parentheses (string) or hex string
            if (content[startIndex] == '(')
            {
                // String literal
                var endIndex = FindClosingParenthesis(content, startIndex);
                if (endIndex > startIndex + 1)
                {
                    var value = content.Substring(startIndex + 1, endIndex - startIndex - 1);
                    return DecodeOctalEscapes(value);
                }
            }
            else if (content[startIndex] == '<')
            {
                // Hex string
                var endIndex = content.IndexOf('>', startIndex);
                if (endIndex > startIndex + 1)
                {
                    var hexValue = content.Substring(startIndex + 1, endIndex - startIndex - 1);
                    return DecodeHexString(hexValue);
                }
            }
        }
        catch { }
        
        return null;
    }

    /// <summary>
    /// Finds the closing parenthesis, handling escapes.
    /// </summary>
    private static int FindClosingParenthesis(string content, int openIndex)
    {
        int depth = 1;
        for (int i = openIndex + 1; i < content.Length && depth > 0; i++)
        {
            if (content[i] == '\\' && i + 1 < content.Length)
            {
                i++; // Skip escaped character
            }
            else if (content[i] == '(')
            {
                depth++;
            }
            else if (content[i] == ')')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return content.Length;
    }

    /// <summary>
    /// Decodes octal escape sequences in PDF strings.
    /// </summary>
    private static string DecodeOctalEscapes(string value)
    {
        // Simple handling of common escapes
        return value
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\(", "(")
            .Replace("\\)", ")")
            .Replace("\\\\", "\\");
    }

    /// <summary>
    /// Decodes a PDF hex string.
    /// </summary>
    private static string? DecodeHexString(string hex)
    {
        try
        {
            hex = hex.Replace(" ", "").Replace("\n", "").Replace("\r", "");
            if (hex.Length % 2 != 0) hex += "0"; // Pad if odd
            
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            
            // Try UTF-16 BE first (common for PDF), then fallback to ASCII
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return System.Text.Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }
            
            return System.Text.Encoding.ASCII.GetString(bytes).Trim('\0');
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a PDF date string (D:YYYYMMDDHHmmSS format).
    /// </summary>
    private static DateTime? ParsePdfDate(string dateString)
    {
        try
        {
            // Remove D: prefix
            if (dateString.StartsWith("D:"))
                dateString = dateString.Substring(2);
            
            // Parse YYYYMMDDHHMMSS
            if (dateString.Length >= 8)
            {
                var year = int.Parse(dateString.Substring(0, 4));
                var month = dateString.Length >= 6 ? int.Parse(dateString.Substring(4, 2)) : 1;
                var day = dateString.Length >= 8 ? int.Parse(dateString.Substring(6, 2)) : 1;
                var hour = dateString.Length >= 10 ? int.Parse(dateString.Substring(8, 2)) : 0;
                var minute = dateString.Length >= 12 ? int.Parse(dateString.Substring(10, 2)) : 0;
                var second = dateString.Length >= 14 ? int.Parse(dateString.Substring(12, 2)) : 0;
                
                return new DateTime(year, month, day, hour, minute, second);
            }
        }
        catch { }
        
        return null;
    }

    /// <summary>
    /// Gets a human-readable page size description based on dimensions.
    /// </summary>
    private static string GetPageSizeDescription(double widthPoints, double heightPoints)
    {
        // Convert points to inches (72 points = 1 inch)
        var widthInches = widthPoints / 72.0;
        var heightInches = heightPoints / 72.0;
        
        // Check for common page sizes (with tolerance)
        const double tolerance = 0.1;
        
        // Always use the larger dimension for comparison (handle landscape)
        var maxDim = Math.Max(widthInches, heightInches);
        var minDim = Math.Min(widthInches, heightInches);
        
        // Letter: 8.5 x 11 inches
        if (Math.Abs(minDim - 8.5) < tolerance && Math.Abs(maxDim - 11.0) < tolerance)
            return "Letter (8.5\" × 11\")";
        
        // Legal: 8.5 x 14 inches
        if (Math.Abs(minDim - 8.5) < tolerance && Math.Abs(maxDim - 14.0) < tolerance)
            return "Legal (8.5\" × 14\")";
        
        // A4: 8.27 x 11.69 inches
        if (Math.Abs(minDim - 8.27) < tolerance && Math.Abs(maxDim - 11.69) < tolerance)
            return "A4 (210mm × 297mm)";
        
        // A3: 11.69 x 16.54 inches
        if (Math.Abs(minDim - 11.69) < tolerance && Math.Abs(maxDim - 16.54) < tolerance)
            return "A3 (297mm × 420mm)";
        
        // A5: 5.83 x 8.27 inches
        if (Math.Abs(minDim - 5.83) < tolerance && Math.Abs(maxDim - 8.27) < tolerance)
            return "A5 (148mm × 210mm)";
        
        // Tabloid: 11 x 17 inches
        if (Math.Abs(minDim - 11.0) < tolerance && Math.Abs(maxDim - 17.0) < tolerance)
            return "Tabloid (11\" × 17\")";
        
        // Custom size - return dimensions
        return $"{widthInches:F1}\" × {heightInches:F1}\"";
    }
}

/// <summary>
/// Container for PDF metadata.
/// </summary>
public class PdfMetadata
{
    // Basic Properties
    public ulong FileSize { get; set; }
    public DateTime? DateModified { get; set; }
    public DateTime? DateCreated { get; set; }
    
    // PDF Document Properties
    public int PageCount { get; set; }
    public bool IsPasswordProtected { get; set; }
    public string? PdfVersion { get; set; }
    
    // Page Information
    public double PageWidth { get; set; }
    public double PageHeight { get; set; }
    public string? PageOrientation { get; set; }
    public string? PageSizeDescription { get; set; }
    
    // Document Metadata
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Subject { get; set; }
    public string? Keywords { get; set; }
    public string? Creator { get; set; }
    public string? Producer { get; set; }
    public DateTime? PdfCreationDate { get; set; }
    public DateTime? PdfModificationDate { get; set; }
    
    /// <summary>
    /// Gets page dimensions in a formatted string.
    /// </summary>
    public string PageDimensions
    {
        get
        {
            if (PageWidth == 0 || PageHeight == 0)
                return "Unknown";
            
            // Convert points to inches
            var widthInches = PageWidth / 72.0;
            var heightInches = PageHeight / 72.0;
            
            return $"{widthInches:F1}\" × {heightInches:F1}\"";
        }
    }
}
