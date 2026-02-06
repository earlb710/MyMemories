using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyMemories.Models;
using MyMemories.Utilities;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Windows.ApplicationModel.DataTransfer;

namespace MyMemories.Services;

/// <summary>
/// Service for extracting tabular data from PDF documents.
/// Uses PdfPig library to parse PDF content and detect table structures.
/// </summary>
public class PdfTableExtractorService
{
    // Constants for table detection
    private const double ColumnGapThreshold = 20.0; // Minimum gap between columns in points
    private const double ColumnMargin = 10.0; // Margin for column boundary detection
    
    /// <summary>
    /// Extracts tables from a PDF file by analyzing text positions and alignment.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <returns>List of extracted tables.</returns>
    public async Task<List<TableData>> ExtractTablesAsync(string pdfPath)
    {
        return await Task.Run(() =>
        {
            var tables = new List<TableData>();

            try
            {
                if (string.IsNullOrEmpty(pdfPath) || !File.Exists(pdfPath))
                {
                    LogUtilities.LogError("PdfTableExtractorService.ExtractTablesAsync", 
                        $"PDF file not found: {pdfPath}", null);
                    return tables;
                }

                using var document = PdfDocument.Open(pdfPath);
                
                LogUtilities.LogInfo($"[PdfTableExtractorService] Processing PDF with {document.NumberOfPages} pages");

                for (int i = 1; i <= document.NumberOfPages; i++)
                {
                    var page = document.GetPage(i);
                    var pageTable = ExtractTableFromPage(page, i);
                    
                    if (pageTable != null && pageTable.RowCount > 0)
                    {
                        tables.Add(pageTable);
                        LogUtilities.LogInfo($"[PdfTableExtractorService] Found table on page {i} with {pageTable.RowCount} rows and {pageTable.ColumnCount} columns");
                    }
                }

                if (tables.Count == 0)
                {
                    LogUtilities.LogInfo("[PdfTableExtractorService] No tables detected in PDF");
                }
            }
            catch (Exception ex)
            {
                LogUtilities.LogError("PdfTableExtractorService.ExtractTablesAsync", 
                    "Error extracting tables from PDF", ex);
            }

            return tables;
        });
    }

    /// <summary>
    /// Extracts a table from a single PDF page by analyzing text positions.
    /// </summary>
    private TableData? ExtractTableFromPage(Page page, int pageNumber)
    {
        try
        {
            var words = page.GetWords().ToList();
            
            if (words.Count == 0)
                return null;

            // Group words by their Y position (rows)
            var rowGroups = words
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                .OrderByDescending(g => g.Key) // Top to bottom
                .ToList();

            if (rowGroups.Count < 2) // Need at least 2 rows for a table
                return null;

            // Analyze column structure from first few rows
            var columnPositions = DetectColumnPositions(rowGroups.Take(Math.Min(5, rowGroups.Count)).ToList());
            
            if (columnPositions.Count < 2) // Need at least 2 columns for a table
                return null;

            // Extract rows
            var tableRows = new List<List<string>>();
            
            foreach (var rowGroup in rowGroups)
            {
                var row = new List<string>();
                var rowWords = rowGroup.OrderBy(w => w.BoundingBox.Left).ToList();
                
                // Assign words to columns based on position
                foreach (var colPos in columnPositions)
                {
                    var cellWords = rowWords
                        .Where(w => IsWordInColumn(w, colPos, columnPositions))
                        .OrderBy(w => w.BoundingBox.Left)
                        .ToList();
                    
                    var cellText = string.Join(" ", cellWords.Select(w => w.Text));
                    row.Add(cellText);
                }
                
                // Only add rows that have at least some content
                if (row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                {
                    tableRows.Add(row);
                }
            }

            if (tableRows.Count == 0)
                return null;

            return new TableData
            {
                PageNumber = pageNumber,
                Rows = tableRows
            };
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("PdfTableExtractorService.ExtractTableFromPage", 
                $"Error extracting table from page {pageNumber}", ex);
            return null;
        }
    }

    /// <summary>
    /// Detects column positions by analyzing word clustering on X-axis.
    /// </summary>
    private List<double> DetectColumnPositions(List<IGrouping<double, Word>> sampleRows)
    {
        var allXPositions = sampleRows
            .SelectMany(row => row.Select(w => w.BoundingBox.Left))
            .OrderBy(x => x)
            .ToList();

        if (allXPositions.Count == 0)
            return new List<double>();

        // Use clustering to find column start positions
        var columns = new List<double>();
        
        var currentCluster = new List<double> { allXPositions[0] };
        
        for (int i = 1; i < allXPositions.Count; i++)
        {
            if (allXPositions[i] - allXPositions[i - 1] < ColumnGapThreshold)
            {
                currentCluster.Add(allXPositions[i]);
            }
            else
            {
                columns.Add(currentCluster.Average());
                currentCluster = new List<double> { allXPositions[i] };
            }
        }
        
        if (currentCluster.Count > 0)
        {
            columns.Add(currentCluster.Average());
        }

        return columns;
    }

    /// <summary>
    /// Determines if a word belongs to a specific column.
    /// </summary>
    private bool IsWordInColumn(Word word, double columnPos, List<double> allColumns)
    {
        var wordLeft = word.BoundingBox.Left;
        var index = allColumns.IndexOf(columnPos);
        
        // Find the range for this column
        double minPos = columnPos - ColumnMargin;
        double maxPos = index < allColumns.Count - 1 ? allColumns[index + 1] - ColumnMargin : double.MaxValue;
        
        return wordLeft >= minPos && wordLeft < maxPos;
    }

    /// <summary>
    /// Exports table data to CSV format.
    /// </summary>
    /// <param name="table">The table data to export.</param>
    /// <param name="outputPath">Path where CSV file should be saved.</param>
    /// <returns>The path of the created CSV file.</returns>
    public async Task<string> ExportToCsvAsync(TableData table, string outputPath)
    {
        try
        {
            var csv = new StringBuilder();
            
            foreach (var row in table.Rows)
            {
                var escapedCells = row.Select(cell => EscapeCsvCell(cell));
                csv.AppendLine(string.Join(",", escapedCells));
            }

            await File.WriteAllTextAsync(outputPath, csv.ToString());
            LogUtilities.LogInfo($"[PdfTableExtractorService] Exported table to CSV: {outputPath}");
            
            return outputPath;
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("PdfTableExtractorService.ExportToCsvAsync", 
                "Error exporting table to CSV", ex);
            throw;
        }
    }

    /// <summary>
    /// Escapes a CSV cell value according to RFC 4180.
    /// </summary>
    private string EscapeCsvCell(string cell)
    {
        if (string.IsNullOrEmpty(cell))
            return "";

        // If cell contains comma, quote, or newline, wrap in quotes and escape internal quotes
        if (cell.Contains(',') || cell.Contains('"') || cell.Contains('\n') || cell.Contains('\r'))
        {
            return $"\"{cell.Replace("\"", "\"\"")}\"";
        }

        return cell;
    }

    /// <summary>
    /// Copies table data to clipboard in tab-separated format.
    /// </summary>
    /// <param name="table">The table data to copy.</param>
    public void CopyToClipboard(TableData table)
    {
        try
        {
            var tsv = new StringBuilder();
            
            foreach (var row in table.Rows)
            {
                tsv.AppendLine(string.Join("\t", row));
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(tsv.ToString());
            Clipboard.SetContent(dataPackage);
            
            LogUtilities.LogInfo("[PdfTableExtractorService] Copied table to clipboard");
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("PdfTableExtractorService.CopyToClipboard", 
                "Error copying table to clipboard", ex);
            throw;
        }
    }
}
