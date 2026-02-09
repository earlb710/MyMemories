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
}
