using System;
using System.Collections.Generic;
using System.Linq;

namespace MyMemories.Models;

/// <summary>
/// Represents a template for a table structure detected in a PDF.
/// Used in two-pass extraction to identify tables with similar formats.
/// </summary>
public class TableTemplate
{
    /// <summary>
    /// Gets or sets the unique identifier for this template.
    /// </summary>
    public int TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the header row text (column names).
    /// </summary>
    public List<string> HeaderRow { get; set; } = new();

    /// <summary>
    /// Gets or sets the column positions (X-coordinates) for this table format.
    /// </summary>
    public List<double> ColumnPositions { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of page numbers where this template was found.
    /// </summary>
    public List<int> PageNumbers { get; set; } = new();

    /// <summary>
    /// Gets the number of columns in this template.
    /// </summary>
    public int ColumnCount => HeaderRow?.Count ?? 0;

    /// <summary>
    /// Calculates similarity score between this template and another header row.
    /// Returns a value between 0.0 (no match) and 1.0 (perfect match).
    /// </summary>
    public double CalculateSimilarity(List<string> otherHeader)
    {
        if (otherHeader == null || otherHeader.Count == 0 || HeaderRow.Count == 0)
            return 0.0;

        // Normalize headers for comparison (lowercase, trim)
        var thisHeaders = HeaderRow.Select(h => h?.ToLowerInvariant().Trim() ?? "").ToList();
        var otherHeaders = otherHeader.Select(h => h?.ToLowerInvariant().Trim() ?? "").ToList();

        // Count matching headers
        int matches = 0;
        int minCount = Math.Min(thisHeaders.Count, otherHeaders.Count);

        for (int i = 0; i < minCount; i++)
        {
            if (thisHeaders[i] == otherHeaders[i])
                matches++;
        }

        // Calculate similarity based on matches and column count similarity
        double matchRatio = (double)matches / Math.Max(thisHeaders.Count, otherHeaders.Count);
        double countRatio = (double)minCount / Math.Max(thisHeaders.Count, otherHeaders.Count);

        // Weight match ratio more heavily
        return (matchRatio * 0.7) + (countRatio * 0.3);
    }

    /// <summary>
    /// Checks if a row of text appears to be a header row based on common keywords.
    /// </summary>
    public static bool IsLikelyHeaderRow(List<string> row)
    {
        if (row == null || row.Count == 0)
            return false;

        var commonHeaderKeywords = new[]
        {
            "date", "description", "amount", "balance", "quantity", "price", "total",
            "name", "id", "number", "type", "status", "category", "item", "product",
            "reference", "transaction", "payment", "debit", "credit", "account",
            "charges", "fees", "accrued", "bank"
        };

        // Normalize row text
        var normalizedRow = row.Select(cell => cell?.ToLowerInvariant().Trim() ?? "").ToList();

        // Count how many cells contain header keywords
        int keywordCount = 0;
        foreach (var cell in normalizedRow)
        {
            if (string.IsNullOrWhiteSpace(cell))
                continue;

            foreach (var keyword in commonHeaderKeywords)
            {
                if (cell.Contains(keyword))
                {
                    keywordCount++;
                    break; // Count each cell only once
                }
            }
        }

        // Consider it a header if at least 2 cells contain keywords and at least 30% of cells match
        return keywordCount >= 2 && (double)keywordCount / row.Count >= 0.3;
    }
    
    /// <summary>
    /// Checks if a row of text appears to be a header row based on common keywords or bold formatting.
    /// This overload accepts Word objects to check font styling.
    /// </summary>
    public static bool IsLikelyHeaderRow(List<string> row, List<UglyToad.PdfPig.Content.Word> words)
    {
        if (row == null || row.Count == 0)
            return false;

        // First check keyword-based detection
        bool hasKeywords = IsLikelyHeaderRow(row);
        if (hasKeywords)
            return true;

        // If no keyword match, check if text is in bold
        // Bold headers are common in PDFs even without standard keywords
        if (words != null && words.Count > 0)
        {
            int boldCount = 0;
            foreach (var word in words)
            {
                if (IsBoldText(word))
                    boldCount++;
            }

            // If >50% of words are bold, likely a header row
            double boldRatio = (double)boldCount / words.Count;
            if (boldRatio > 0.5)
            {
                // Log that we detected a bold header
                try
                {
                    MyMemories.Utilities.LogUtilities.LogInfo("TableTemplate.IsLikelyHeaderRow", 
                        $"Detected bold header row: {string.Join(", ", row.Take(3))}... ({boldCount}/{words.Count} bold words)");
                }
                catch { /* Logging failure shouldn't break detection */ }
                
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a Word is rendered in bold font.
    /// Examines font names and properties to detect bold styling.
    /// </summary>
    private static bool IsBoldText(UglyToad.PdfPig.Content.Word word)
    {
        try
        {
            // Check letters in the word for font information
            var letters = word.Letters;
            if (letters == null || letters.Count == 0)
                return false;

            // Check if any letter has a bold font
            // Font names often contain "Bold", "Heavy", "Black", "Demi", "SemiBold"
            foreach (var letter in letters)
            {
                var fontName = letter.FontName?.ToUpperInvariant() ?? "";
                
                if (fontName.Contains("BOLD") ||
                    fontName.Contains("HEAVY") ||
                    fontName.Contains("BLACK") ||
                    fontName.Contains("DEMI") ||
                    fontName.Contains("SEMIBOLD"))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // If we can't determine, assume not bold
            return false;
        }
    }
}
