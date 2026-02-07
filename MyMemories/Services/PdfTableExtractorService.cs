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
    private const double ColumnGapThreshold = 15.0; // Minimum gap between columns in points (increased to reduce over-detection)
    private const double ColumnMargin = 8.0; // Margin for column boundary detection (increased for better grouping)
    private const double MinColumnOccupancy = 0.10; // Minimum fraction of rows that must have data in a column (10% - reduced to handle smaller tables)
    private const double VerticalTextThreshold = 45.0; // Degrees - text rotated more than this is considered vertical
    private const double MinRowDensity = 0.15; // Minimum fraction of columns that must have data in a row (15%) - reduced to prevent filtering valid rows
    
    // Constants for line-based table detection
    private const double MinVerticalLineLength = 15.0; // Minimum length for a line to be considered a column separator (in points)
    private const double VerticalLineAngleTolerance = 5.0; // Degrees - maximum deviation from vertical for a line to be considered vertical
    private const double LineMergeTolerance = 2.0; // Points - lines within this distance are considered the same column boundary
    
    // Constants for table segmentation (detecting multiple tables on same page)
    private const double MinVerticalGapForTableSplit = 20.0; // Minimum vertical gap (points) between rows to consider a table boundary
    private const double ColumnStructureChangeThreshold = 0.4; // If column positions differ by >40%, consider it a new table
    
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
                
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTablesAsync", 
                    $"Processing PDF with {document.NumberOfPages} pages");

                for (int i = 1; i <= document.NumberOfPages; i++)
                {
                    var page = document.GetPage(i);
                    var pageTables = ExtractTablesFromPage(page, i);
                    
                    if (pageTables.Count > 0)
                    {
                        tables.AddRange(pageTables);
                        LogUtilities.LogInfo("PdfTableExtractorService.ExtractTablesAsync", 
                            $"Found {pageTables.Count} table(s) on page {i}");
                        
                        for (int t = 0; t < pageTables.Count; t++)
                        {
                            LogUtilities.LogInfo("PdfTableExtractorService.ExtractTablesAsync", 
                                $"  Table {t + 1}: {pageTables[t].RowCount} rows, {pageTables[t].ColumnCount} columns");
                        }
                    }
                }

                if (tables.Count == 0)
                {
                    LogUtilities.LogInfo("PdfTableExtractorService.ExtractTablesAsync", 
                        "No tables detected in PDF");
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
    /// Extracts multiple tables from a single PDF page by detecting table regions.
    /// This handles pages with multiple distinct tables (e.g., summary table + transaction table).
    /// </summary>
    private List<TableData> ExtractTablesFromPage(Page page, int pageNumber)
    {
        var tables = new List<TableData>();
        
        try
        {
            var words = page.GetWords().ToList();
            
            if (words.Count == 0)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTablesFromPage", 
                    $"Page {pageNumber}: No words found");
                return tables;
            }

            LogUtilities.LogInfo("PdfTableExtractorService.ExtractTablesFromPage", 
                $"Page {pageNumber}: Found {words.Count} words");

            // Filter out vertical/rotated text
            var horizontalWords = FilterHorizontalText(words, pageNumber);
            
            if (horizontalWords.Count == 0)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTablesFromPage", 
                    $"Page {pageNumber}: No horizontal words after filtering");
                return tables;
            }

            LogUtilities.LogInfo("PdfTableExtractorService.ExtractTablesFromPage", 
                $"Page {pageNumber}: {horizontalWords.Count} horizontal words");

            // Group words by Y position (rows)
            var rowGroups = horizontalWords
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                .OrderByDescending(g => g.Key) // Top to bottom
                .Select(g => (dynamic)new { YPosition = g.Key, Words = g.ToList() })
                .ToList();

            if (rowGroups.Count < 1)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTablesFromPage", 
                    $"Page {pageNumber}: No rows found");
                return tables;
            }

            // Detect table regions by finding large vertical gaps or column structure changes
            var tableRegions = DetectTableRegions(rowGroups, pageNumber);
            
            LogUtilities.LogInfo("PdfTableExtractorService.ExtractTablesFromPage", 
                $"Page {pageNumber}: Detected {tableRegions.Count} table region(s)");

            // Extract each table region separately
            for (int regionIndex = 0; regionIndex < tableRegions.Count; regionIndex++)
            {
                var region = tableRegions[regionIndex];
                var regionTable = ExtractTableFromRegion(region, page, pageNumber, regionIndex + 1);
                
                if (regionTable != null && regionTable.RowCount > 0)
                {
                    tables.Add(regionTable);
                }
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("PdfTableExtractorService.ExtractTablesFromPage", 
                $"Error extracting tables from page {pageNumber}", ex);
        }

        return tables;
    }

    /// <summary>
    /// Detects distinct table regions on a page by analyzing vertical gaps and column structure.
    /// </summary>
    private List<List<dynamic>> DetectTableRegions(List<dynamic> rowGroups, int pageNumber)
    {
        var regions = new List<List<dynamic>>();
        var currentRegion = new List<dynamic>();

        for (int i = 0; i < rowGroups.Count; i++)
        {
            var currentRow = rowGroups[i];
            currentRegion.Add(currentRow);

            // Check if this is the last row or if there's a large gap to the next row
            if (i < rowGroups.Count - 1)
            {
                var nextRow = rowGroups[i + 1];
                double verticalGap = currentRow.YPosition - nextRow.YPosition;

                // If large vertical gap detected, consider this a table boundary
                if (verticalGap > MinVerticalGapForTableSplit)
                {
                    LogUtilities.LogInfo("PdfTableExtractorService.DetectTableRegions", 
                        $"Page {pageNumber}: Large vertical gap detected ({verticalGap:F1} pts) - splitting table");
                    
                    if (currentRegion.Count >= 1) // Need at least 1 row
                    {
                        regions.Add(new List<dynamic>(currentRegion));
                    }
                    currentRegion.Clear();
                }
            }
        }

        // Add the last region if it has rows
        if (currentRegion.Count >= 1)
        {
            regions.Add(new List<dynamic>(currentRegion));
        }

        return regions;
    }

    /// <summary>
    /// Extracts a table from a specific region of rows on a page.
    /// </summary>
    private TableData? ExtractTableFromRegion(List<dynamic> rowGroups, Page page, int pageNumber, int regionNumber)
    {
        try
        {
            LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromRegion", 
                $"Page {pageNumber}, Region {regionNumber}: Processing {rowGroups.Count} rows");

            // First, try to detect column boundaries from vertical lines in the PDF
            var columnPositions = DetectVerticalLinesFromPaths(page, pageNumber);
            
            // If no vertical lines found, fall back to text-based column detection
            if (columnPositions.Count == 0)
            {
                // Analyze column structure from rows in this region
                var sampleRowCount = Math.Min(10, rowGroups.Count);
                var sampleRows = rowGroups.Take(sampleRowCount)
                    .Select(rg => rg.Words as List<Word>)
                    .Select(words => words.GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1)).First())
                    .ToList();
                
                columnPositions = DetectColumnPositions(sampleRows);
                
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromRegion", 
                    $"Page {pageNumber}, Region {regionNumber}: Detected {columnPositions.Count} columns (text-based)");
            }
            else
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromRegion", 
                    $"Page {pageNumber}, Region {regionNumber}: Using {columnPositions.Count} columns (line-based)");
            }
            
            if (columnPositions.Count < 1)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromRegion", 
                    $"Page {pageNumber}, Region {regionNumber}: Not enough columns detected");
                return null;
            }

            // Extract rows
            var tableRows = new List<List<string>>();
            
            foreach (var rowGroup in rowGroups)
            {
                var row = new List<string>();
                var rowWords = (rowGroup.Words as List<Word>).OrderBy(w => w.BoundingBox.Left).ToList();
                
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
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromRegion", 
                    $"Page {pageNumber}, Region {regionNumber}: No valid rows after processing");
                return null;
            }

            // Remove empty/sparse columns
            tableRows = RemoveEmptyColumns(tableRows, pageNumber);
            
            if (tableRows.Count == 0 || tableRows[0].Count == 0)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromRegion", 
                    $"Page {pageNumber}, Region {regionNumber}: No valid columns after filtering");
                return null;
            }

            // Remove sparse rows
            tableRows = RemoveSparseRows(tableRows, pageNumber);
            
            if (tableRows.Count == 0)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromRegion", 
                    $"Page {pageNumber}, Region {regionNumber}: No valid rows after filtering");
                return null;
            }

            LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromRegion", 
                $"Page {pageNumber}, Region {regionNumber}: Successfully extracted {tableRows.Count} rows with {tableRows[0].Count} columns");

            return new TableData
            {
                PageNumber = pageNumber,
                Rows = tableRows
            };
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("PdfTableExtractorService.ExtractTableFromRegion", 
                $"Error extracting table from page {pageNumber}, region {regionNumber}", ex);
            return null;
        }
    }

    /// <summary>
    /// Extracts a table from a single PDF page by analyzing text positions.
    /// DEPRECATED: Use ExtractTablesFromPage instead which handles multiple tables per page.
    /// </summary>
    private TableData? ExtractTableFromPage(Page page, int pageNumber)
    {
        try
        {
            var words = page.GetWords().ToList();
            
            if (words.Count == 0)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromPage", 
                    $"Page {pageNumber}: No words found");
                return null;
            }

            LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromPage", 
                $"Page {pageNumber}: Found {words.Count} words");

            // Filter out vertical/rotated text (it's usually labels, not table data)
            var horizontalWords = FilterHorizontalText(words, pageNumber);
            
            if (horizontalWords.Count == 0)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromPage", 
                    $"Page {pageNumber}: No horizontal words after filtering");
                return null;
            }

            LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromPage", 
                $"Page {pageNumber}: {horizontalWords.Count} horizontal words ({words.Count - horizontalWords.Count} vertical/rotated filtered)");

            // Group words by their Y position (rows)
            var rowGroups = horizontalWords
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                .OrderByDescending(g => g.Key) // Top to bottom
                .ToList();

            LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromPage", 
                $"Page {pageNumber}: Grouped into {rowGroups.Count} potential rows");

            if (rowGroups.Count < 1) // Need at least 1 row (relaxed from 2)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromPage", 
                    $"Page {pageNumber}: Not enough rows for a table");
                return null;
            }

            // First, try to detect column boundaries from vertical lines in the PDF
            var columnPositions = DetectVerticalLinesFromPaths(page, pageNumber);
            
            // If no vertical lines found, fall back to text-based column detection
            if (columnPositions.Count == 0)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromPage", 
                    $"Page {pageNumber}: No vertical lines found, using text-based column detection");
                
                // Analyze column structure from first few rows (or all rows if less than 10)
                var sampleRowCount = Math.Min(10, rowGroups.Count); // Increased from 5 to 10
                columnPositions = DetectColumnPositions(rowGroups.Take(sampleRowCount).ToList());
                
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromPage", 
                    $"Page {pageNumber}: Detected {columnPositions.Count} columns from {sampleRowCount} sample rows (text-based)");
            }
            else
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromPage", 
                    $"Page {pageNumber}: Using {columnPositions.Count} columns from vertical line detection");
            }
            
            if (columnPositions.Count < 1) // Need at least 1 column (relaxed from 2)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromPage", 
                    $"Page {pageNumber}: Not enough columns detected");
                return null;
            }

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
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromPage", 
                    $"Page {pageNumber}: No valid rows after processing");
                return null;
            }

            // Remove empty/sparse columns
            tableRows = RemoveEmptyColumns(tableRows, pageNumber);
            
            if (tableRows.Count == 0 || tableRows[0].Count == 0)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromPage", 
                    $"Page {pageNumber}: No valid columns after filtering empty columns");
                return null;
            }

            // Remove sparse rows (rows with mostly empty cells - typically headers/footers)
            tableRows = RemoveSparseRows(tableRows, pageNumber);
            
            if (tableRows.Count == 0)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromPage", 
                    $"Page {pageNumber}: No valid rows after filtering sparse rows");
                return null;
            }

            LogUtilities.LogInfo("PdfTableExtractorService.ExtractTableFromPage", 
                $"Page {pageNumber}: Successfully extracted {tableRows.Count} rows with {tableRows[0].Count} columns");

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
    /// Filters out vertical or rotated text, keeping only horizontal text for table extraction.
    /// Vertical text is often used for labels/headers and causes issues with row grouping.
    /// </summary>
    private List<Word> FilterHorizontalText(List<Word> words, int pageNumber)
    {
        var horizontalWords = new List<Word>();
        int verticalCount = 0;
        
        foreach (var word in words)
        {
            // Check if the word contains any letters to determine rotation
            var letters = word.Letters;
            if (letters.Any())
            {
                // Calculate average rotation of letters in the word
                var avgRotation = letters.Average(l => Math.Abs(l.GlyphRectangle.Rotation));
                
                // Keep words that are approximately horizontal (rotation close to 0 or 180)
                if (avgRotation < VerticalTextThreshold || avgRotation > (180 - VerticalTextThreshold))
                {
                    horizontalWords.Add(word);
                }
                else
                {
                    verticalCount++;
                }
            }
            else
            {
                // If no letters, keep the word (might be numbers/symbols)
                horizontalWords.Add(word);
            }
        }
        
        if (verticalCount > 0)
        {
            LogUtilities.LogInfo("PdfTableExtractorService.FilterHorizontalText", 
                $"Page {pageNumber}: Filtered {verticalCount} vertical/rotated words");
        }
        
        return horizontalWords;
    }

    /// <summary>
    /// Removes columns that are mostly empty (sparse columns with little data).
    /// This helps eliminate columns created from marginal text or spacing variations.
    /// Uses adaptive threshold: more lenient for smaller tables.
    /// </summary>
    private List<List<string>> RemoveEmptyColumns(List<List<string>> rows, int pageNumber)
    {
        if (rows.Count == 0 || rows[0].Count == 0)
            return rows;
        
        int columnCount = rows[0].Count;
        var columnsToKeep = new List<int>();
        
        // Use adaptive threshold: more lenient for smaller tables
        // For tables with < 10 rows, require only 1 non-empty cell
        // For larger tables, use the MinColumnOccupancy threshold
        double effectiveThreshold = rows.Count < 10 
            ? (1.0 / rows.Count)  // At least 1 cell must have data
            : MinColumnOccupancy;
        
        LogUtilities.LogInfo("PdfTableExtractorService.RemoveEmptyColumns", 
            $"Page {pageNumber}: Using occupancy threshold {effectiveThreshold:P0} for {rows.Count} rows");
        
        // Check each column for occupancy
        for (int col = 0; col < columnCount; col++)
        {
            int nonEmptyCount = 0;
            
            foreach (var row in rows)
            {
                if (col < row.Count && !string.IsNullOrWhiteSpace(row[col]))
                {
                    nonEmptyCount++;
                }
            }
            
            double occupancy = (double)nonEmptyCount / rows.Count;
            
            // Keep column if it meets minimum occupancy threshold
            if (occupancy >= effectiveThreshold)
            {
                columnsToKeep.Add(col);
                LogUtilities.LogInfo("PdfTableExtractorService.RemoveEmptyColumns", 
                    $"Page {pageNumber}: Column {col} kept (occupancy: {occupancy:P0}, {nonEmptyCount}/{rows.Count} rows)");
            }
            else
            {
                LogUtilities.LogInfo("PdfTableExtractorService.RemoveEmptyColumns", 
                    $"Page {pageNumber}: Column {col} removed (occupancy: {occupancy:P0}, {nonEmptyCount}/{rows.Count} rows)");
            }
        }
        
        // If we would remove all columns, keep all of them (fallback)
        if (columnsToKeep.Count == 0)
        {
            LogUtilities.LogInfo("PdfTableExtractorService.RemoveEmptyColumns", 
                $"Page {pageNumber}: Would remove all columns, keeping all instead");
            return rows;
        }
        
        // If we're removing some columns, log it
        if (columnsToKeep.Count < columnCount)
        {
            LogUtilities.LogInfo("PdfTableExtractorService.RemoveEmptyColumns", 
                $"Page {pageNumber}: Removing {columnCount - columnsToKeep.Count} sparse columns (keeping {columnsToKeep.Count})");
        }
        
        // Create new rows with only the columns we want to keep
        var filteredRows = new List<List<string>>();
        foreach (var row in rows)
        {
            var filteredRow = new List<string>();
            foreach (int colIndex in columnsToKeep)
            {
                if (colIndex < row.Count)
                {
                    filteredRow.Add(row[colIndex]);
                }
                else
                {
                    filteredRow.Add("");
                }
            }
            filteredRows.Add(filteredRow);
        }
        
        return filteredRows;
    }

    /// <summary>
    /// Removes rows that are mostly empty (sparse rows with little data).
    /// This helps eliminate header/footer text and other non-table content.
    /// A row must have data in at least MinRowDensity% of columns to be kept.
    /// Uses adaptive threshold: more lenient when there are fewer rows to avoid over-filtering.
    /// </summary>
    private List<List<string>> RemoveSparseRows(List<List<string>> rows, int pageNumber)
    {
        if (rows.Count == 0)
            return rows;
        
        int columnCount = rows[0].Count;
        var rowsToKeep = new List<List<string>>();
        int removedCount = 0;
        
        // Use adaptive threshold based on row count
        // If we have very few rows (< 10), be more lenient to avoid filtering everything
        double effectiveThreshold = rows.Count < 10 
            ? 0.20  // 20% for small sets (avoid over-filtering continuation pages)
            : MinRowDensity;  // 30% for larger sets
        
        LogUtilities.LogInfo("PdfTableExtractorService.RemoveSparseRows", 
            $"Page {pageNumber}: Using row density threshold {effectiveThreshold:P0} for {rows.Count} rows");
        
        foreach (var row in rows)
        {
            // Count non-empty cells in this row
            int nonEmptyCount = row.Count(cell => !string.IsNullOrWhiteSpace(cell));
            double density = (double)nonEmptyCount / columnCount;
            
            // Keep row if it meets minimum density threshold
            if (density >= effectiveThreshold)
            {
                rowsToKeep.Add(row);
            }
            else
            {
                removedCount++;
                LogUtilities.LogInfo("PdfTableExtractorService.RemoveSparseRows", 
                    $"Page {pageNumber}: Removed sparse row with density {density:P0} ({nonEmptyCount}/{columnCount} cells)");
            }
        }
        
        // If we would remove everything, keep all rows (fallback to avoid losing data)
        if (rowsToKeep.Count == 0 && rows.Count > 0)
        {
            LogUtilities.LogInfo("PdfTableExtractorService.RemoveSparseRows", 
                $"Page {pageNumber}: Would remove all {rows.Count} rows, keeping all instead");
            return rows;
        }
        
        if (removedCount > 0)
        {
            LogUtilities.LogInfo("PdfTableExtractorService.RemoveSparseRows", 
                $"Page {pageNumber}: Removed {removedCount} sparse rows (kept {rowsToKeep.Count})");
        }
        
        return rowsToKeep;
    }

    /// <summary>
    /// Extracts vertical lines from PDF page that could represent column boundaries.
    /// Uses PdfPig's path extraction to find graphical table borders.
    /// </summary>
    private List<double> DetectVerticalLinesFromPaths(Page page, int pageNumber)
    {
        var verticalLines = new List<double>();
        
        try
        {
            // Try to access experimental paths API to get graphical elements
            // This may not be available in all PdfPig versions
            var experimentalAccess = page.ExperimentalAccess;
            if (experimentalAccess == null)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.DetectVerticalLinesFromPaths",
                    $"Page {pageNumber}: ExperimentalAccess not available");
                return verticalLines;
            }
            
            var paths = experimentalAccess.Paths;
            if (paths == null || !paths.Any())
            {
                LogUtilities.LogInfo("PdfTableExtractorService.DetectVerticalLinesFromPaths",
                    $"Page {pageNumber}: No paths found in PDF");
                return verticalLines;
            }
            
            LogUtilities.LogInfo("PdfTableExtractorService.DetectVerticalLinesFromPaths",
                $"Page {pageNumber}: Found {paths.Count} paths to analyze");
            
            // Try to extract line segments from paths
            // The exact API may vary by PdfPig version, so we use defensive coding
            foreach (var path in paths)
            {
                try
                {
                    // Access path commands/segments via reflection for compatibility
                    var commandsProp = path.GetType().GetProperty("Commands");
                    if (commandsProp == null)
                    {
                        // Commands property not available in this PdfPig version
                        LogUtilities.LogInfo("PdfTableExtractorService.DetectVerticalLinesFromPaths", 
                            $"Page {pageNumber}: Commands property not available on PdfPath, skipping line detection");
                        return new List<double>();
                    }
                    
                    var commands = commandsProp.GetValue(path);
                    if (commands == null) continue;
                    
                    // Safely cast to enumerable
                    if (!(commands is System.Collections.IEnumerable enumerable)) continue;
                    
                    // Iterate through commands looking for lines
                    foreach (var command in enumerable)
                    {
                        // Try to extract line coordinates
                        // Different PdfPig versions may have different command types
                        var commandType = command.GetType().Name;
                        
                        // Look for line-like commands
                        if (commandType.Contains("Line") || commandType.Contains("line"))
                        {
                            // Try to get start and end points via reflection if needed
                            var startProp = command.GetType().GetProperty("Start");
                            var endProp = command.GetType().GetProperty("End");
                            
                            if (startProp != null && endProp != null)
                            {
                                var start = startProp.GetValue(command) as UglyToad.PdfPig.Core.PdfPoint?;
                                var end = endProp.GetValue(command) as UglyToad.PdfPig.Core.PdfPoint?;
                                
                                if (start.HasValue && end.HasValue)
                                {
                                    var x1 = start.Value.X;
                                    var y1 = start.Value.Y;
                                    var x2 = end.Value.X;
                                    var y2 = end.Value.Y;
                                    
                                    // Check if line is vertical (X positions very close)
                                    var xDiff = Math.Abs(x2 - x1);
                                    var yDiff = Math.Abs(y2 - y1);
                                    var lineLength = Math.Sqrt(xDiff * xDiff + yDiff * yDiff);
                                    
                                    // Vertical line: X values similar, Y values different, and long enough
                                    if (xDiff <= VerticalLineAngleTolerance && 
                                        yDiff > xDiff && 
                                        lineLength >= MinVerticalLineLength)
                                    {
                                        // Use average X position as column boundary
                                        var xPos = (x1 + x2) / 2.0;
                                        verticalLines.Add(xPos);
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Silently skip paths that can't be processed
                    continue;
                }
            }
            
            if (verticalLines.Count == 0)
            {
                LogUtilities.LogInfo("PdfTableExtractorService.DetectVerticalLinesFromPaths",
                    $"Page {pageNumber}: No vertical lines detected from paths");
                return verticalLines;
            }
            
            LogUtilities.LogInfo("PdfTableExtractorService.DetectVerticalLinesFromPaths",
                $"Page {pageNumber}: Found {verticalLines.Count} vertical line segments");
            
            // Sort and merge nearby lines (they might be the same column boundary drawn multiple times)
            verticalLines = verticalLines.OrderBy(x => x).ToList();
            var mergedLines = new List<double>();
            
            if (verticalLines.Count > 0)
            {
                mergedLines.Add(verticalLines[0]);
                
                for (int i = 1; i < verticalLines.Count; i++)
                {
                    // If this line is close to the previous merged line, merge them (duplicate)
                    if (verticalLines[i] - mergedLines[mergedLines.Count - 1] < LineMergeTolerance)
                    {
                        // Update to average position
                        mergedLines[mergedLines.Count - 1] = (mergedLines[mergedLines.Count - 1] + verticalLines[i]) / 2.0;
                    }
                    else
                    {
                        mergedLines.Add(verticalLines[i]);
                    }
                }
            }
            
            LogUtilities.LogInfo("PdfTableExtractorService.DetectVerticalLinesFromPaths",
                $"Page {pageNumber}: Detected {mergedLines.Count} column boundaries after merging");
            
            return mergedLines;
        }
        catch (Exception ex)
        {
            LogUtilities.LogInfo("PdfTableExtractorService.DetectVerticalLinesFromPaths",
                $"Page {pageNumber}: Could not extract vertical lines (will use text-based detection): {ex.Message}");
            return verticalLines;
        }
    }

    /// <summary>
    /// Detects column positions by analyzing word clustering on X-axis.
    /// Improved to better handle closely-spaced numeric columns and avoid creating too many empty columns.
    /// Only creates columns that appear consistently across multiple rows.
    /// </summary>
    private List<double> DetectColumnPositions(List<IGrouping<double, Word>> sampleRows)
    {
        if (sampleRows.Count == 0)
            return new List<double>();

        // Count how many rows each X position appears in
        var xPositionFrequency = new Dictionary<double, int>();
        
        foreach (var row in sampleRows)
        {
            // Get unique X positions in this row (rounded to 1 point precision to avoid over-detecting columns)
            var rowXPositions = row.Select(w => Math.Round(w.BoundingBox.Left))
                                   .Distinct()
                                   .ToList();
            
            foreach (var x in rowXPositions)
            {
                if (!xPositionFrequency.ContainsKey(x))
                    xPositionFrequency[x] = 0;
                xPositionFrequency[x]++;
            }
        }
        
        // Only keep X positions that appear in multiple rows (at least 30% of sample rows to filter noise)
        int minFrequency = Math.Max(1, (int)(sampleRows.Count * 0.30));
        var significantXPositions = xPositionFrequency
            .Where(kvp => kvp.Value >= minFrequency)
            .Select(kvp => kvp.Key)
            .OrderBy(x => x)
            .ToList();
        
        LogUtilities.LogInfo("PdfTableExtractorService.DetectColumnPositions", 
            $"Found {significantXPositions.Count} significant X positions (from {xPositionFrequency.Count} total, min frequency: {minFrequency})");

        if (significantXPositions.Count == 0)
            return new List<double>();

        // Use clustering to find column start positions
        var columns = new List<double>();
        
        var currentCluster = new List<double> { significantXPositions[0] };
        
        for (int i = 1; i < significantXPositions.Count; i++)
        {
            double gap = significantXPositions[i] - significantXPositions[i - 1];
            
            // Use adaptive threshold: smaller gap for positions that appear consistently
            // This helps separate closely-spaced numeric columns
            if (gap < ColumnGapThreshold)
            {
                currentCluster.Add(significantXPositions[i]);
            }
            else
            {
                // Create column from cluster
                columns.Add(currentCluster.Average());
                currentCluster = new List<double> { significantXPositions[i] };
            }
        }
        
        if (currentCluster.Count > 0)
        {
            columns.Add(currentCluster.Average());
        }

        // Post-process: merge columns that are too close (likely same column with slight variations)
        var mergedColumns = new List<double>();
        if (columns.Count > 0)
        {
            mergedColumns.Add(columns[0]);
            
            for (int i = 1; i < columns.Count; i++)
            {
                // If this column is within 5 points of the previous, merge them (slight alignment variations)
                if (columns[i] - mergedColumns[mergedColumns.Count - 1] < 5.0)
                {
                    mergedColumns[mergedColumns.Count - 1] = (mergedColumns[mergedColumns.Count - 1] + columns[i]) / 2;
                }
                else
                {
                    mergedColumns.Add(columns[i]);
                }
            }
        }

        return mergedColumns;
    }

    /// <summary>
    /// Determines if a word belongs to a specific column.
    /// Uses midpoints between columns as boundaries.
    /// </summary>
    private bool IsWordInColumn(Word word, double columnPos, List<double> allColumns)
    {
        var wordLeft = word.BoundingBox.Left;
        var index = allColumns.IndexOf(columnPos);
        
        // Calculate column boundaries using midpoints between adjacent columns
        double minPos;
        double maxPos;
        
        if (index == 0)
        {
            // First column: from negative infinity to midpoint with next column
            minPos = double.MinValue;
        }
        else
        {
            // Use midpoint between this column and previous column
            minPos = (allColumns[index - 1] + columnPos) / 2.0;
        }
        
        if (index == allColumns.Count - 1)
        {
            // Last column: from midpoint with previous to positive infinity
            maxPos = double.MaxValue;
        }
        else
        {
            // Use midpoint between this column and next column
            maxPos = (columnPos + allColumns[index + 1]) / 2.0;
        }
        
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
            LogUtilities.LogInfo("PdfTableExtractorService.ExportToCsvAsync", 
                $"Exported table to CSV: {outputPath}");
            
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
            
            LogUtilities.LogInfo("PdfTableExtractorService.CopyToClipboard", 
                "Copied table to clipboard");
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("PdfTableExtractorService.CopyToClipboard", 
                "Error copying table to clipboard", ex);
            throw;
        }
    }

    /// <summary>
    /// Exports all tables to a single CSV file.
    /// </summary>
    public async Task<string> ExportAllToCsvAsync(List<TableData> tables, string outputPath)
    {
        try
        {
            var csv = new StringBuilder();
            
            for (int i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                
                // Add separator header between tables
                if (i > 0)
                {
                    csv.AppendLine(); // Blank line separator
                    csv.AppendLine($"--- Table {i + 1} (Page {table.PageNumber}) ---");
                }
                else
                {
                    csv.AppendLine($"--- Table {i + 1} (Page {table.PageNumber}) ---");
                }
                
                // Add table data
                foreach (var row in table.Rows)
                {
                    var escapedCells = row.Select(cell => EscapeCsvCell(cell));
                    csv.AppendLine(string.Join(",", escapedCells));
                }
            }

            await File.WriteAllTextAsync(outputPath, csv.ToString());
            LogUtilities.LogInfo("PdfTableExtractorService.ExportAllToCsvAsync", 
                $"Exported {tables.Count} tables to CSV: {outputPath}");
            
            return outputPath;
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("PdfTableExtractorService.ExportAllToCsvAsync", 
                "Error exporting tables to CSV", ex);
            throw;
        }
    }

    /// <summary>
    /// Copies all tables to clipboard in tab-separated format.
    /// </summary>
    public void CopyAllToClipboard(List<TableData> tables)
    {
        try
        {
            var tsv = new StringBuilder();
            
            for (int i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                
                // Add separator header between tables
                if (i > 0)
                {
                    tsv.AppendLine(); // Blank line separator
                    tsv.AppendLine($"--- Table {i + 1} (Page {table.PageNumber}) ---");
                }
                else
                {
                    tsv.AppendLine($"--- Table {i + 1} (Page {table.PageNumber}) ---");
                }
                
                // Add table data
                foreach (var row in table.Rows)
                {
                    tsv.AppendLine(string.Join("\t", row));
                }
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(tsv.ToString());
            Clipboard.SetContent(dataPackage);
            
            LogUtilities.LogInfo("PdfTableExtractorService.CopyAllToClipboard", 
                $"Copied {tables.Count} tables to clipboard");
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("PdfTableExtractorService.CopyAllToClipboard", 
                "Error copying tables to clipboard", ex);
            throw;
        }
    }
}
