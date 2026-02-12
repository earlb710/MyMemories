using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyMemories.Models;
using MyMemories.Utilities;
using Tabula;
using Tabula.Detectors;
using Tabula.Extractors;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis;

namespace MyMemories.Services
{
    /// <summary>
    /// Table extraction service using Tabula-sharp for accurate table structure detection.
    /// </summary>
    public class TabulaTableExtractorService
    {
        /// <summary>
        /// Extracts tables from a PDF file using Tabula-sharp algorithms.
        /// </summary>
        public async Task<List<TableData>> ExtractTablesAsync(string pdfPath)
        {
            return await Task.Run(() =>
            {
                var tables = new List<TableData>();

                try
                {
                    LogUtilities.LogInfo("TabulaTableExtractorService.ExtractTablesAsync", $"Processing PDF with Tabula-sharp: {pdfPath}");

                    using (PdfDocument document = PdfDocument.Open(pdfPath, new ParsingOptions() { ClipPaths = true }))
                    {
                        LogUtilities.LogInfo("TabulaTableExtractorService.ExtractTablesAsync", $"PDF has {document.NumberOfPages} page(s)");

                        for (int pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
                        {
                            var pageTables = ExtractTablesFromPage(document, pageNumber);
                            tables.AddRange(pageTables);
                        }
                    }

                    LogUtilities.LogInfo("TabulaTableExtractorService.ExtractTablesAsync", $"Successfully extracted {tables.Count} table(s) using Tabula-sharp");
                }
                catch (Exception ex)
                {
                    LogUtilities.LogError("TabulaTableExtractorService.ExtractTablesAsync", $"Error extracting tables with Tabula-sharp: {ex.Message}");
                }

                return tables;
            });
        }

        /// <summary>
        /// Extracts tables from a single page using Tabula-sharp.
        /// </summary>
        private List<TableData> ExtractTablesFromPage(PdfDocument document, int pageNumber)
        {
            var tables = new List<TableData>();

            try
            {
                LogUtilities.LogInfo("TabulaTableExtractorService.ExtractTablesFromPage", $"Extracting tables from page {pageNumber}");

                // Extract page area
                PageArea page = ObjectExtractor.Extract(document, pageNumber);

                // Try Lattice mode first (for tables with borders/lines)
                var latticeTables = TryLatticeExtraction(page, pageNumber);
                if (latticeTables.Count > 0)
                {
                    LogUtilities.LogInfo("TabulaTableExtractorService.ExtractTablesFromPage", $"Page {pageNumber}: Found {latticeTables.Count} table(s) using Lattice mode");
                    tables.AddRange(latticeTables);
                    return tables;
                }

                // Try Stream mode (for tables without borders)
                var streamTables = TryStreamExtraction(page, pageNumber);
                if (streamTables.Count > 0)
                {
                    LogUtilities.LogInfo("TabulaTableExtractorService.ExtractTablesFromPage", $"Page {pageNumber}: Found {streamTables.Count} table(s) using Stream mode");
                    tables.AddRange(streamTables);
                    return tables;
                }

                LogUtilities.LogInfo("TabulaTableExtractorService.ExtractTablesFromPage", $"Page {pageNumber}: No tables detected by Tabula-sharp");
            }
            catch (Exception ex)
            {
                LogUtilities.LogError("TabulaTableExtractorService.ExtractTablesFromPage", $"Error on page {pageNumber}: {ex.Message}");
            }

            return tables;
        }

        /// <summary>
        /// Try extracting tables using Lattice mode (for bordered tables).
        /// </summary>
        private List<TableData> TryLatticeExtraction(PageArea page, int pageNumber)
        {
            var tables = new List<TableData>();

            try
            {
                IExtractionAlgorithm extractor = new SpreadsheetExtractionAlgorithm();
                var tabulaTables = extractor.Extract(page);

                foreach (var tabulaTable in tabulaTables)
                {
                    if (tabulaTable.RowCount > 0)
                    {
                        var tableData = ConvertTabulaTableToTableData(tabulaTable, pageNumber);
                        tables.Add(tableData);
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtilities.LogInfo("TabulaTableExtractorService.TryLatticeExtraction", $"Lattice extraction failed: {ex.Message}");
            }

            return tables;
        }

        /// <summary>
        /// Try extracting tables using Stream mode (for non-bordered tables).
        /// </summary>
        private List<TableData> TryStreamExtraction(PageArea page, int pageNumber)
        {
            var tables = new List<TableData>();

            try
            {
                // Detect table areas
                var detector = new SimpleNurminenDetectionAlgorithm();
                var regions = detector.Detect(page);

                if (regions.Count == 0)
                {
                    // No regions detected, try extracting from whole page
                    IExtractionAlgorithm extractor = new BasicExtractionAlgorithm();
                    var tabulaTables = extractor.Extract(page);

                    foreach (var tabulaTable in tabulaTables)
                    {
                        if (tabulaTable.RowCount > 0)
                        {
                            var tableData = ConvertTabulaTableToTableData(tabulaTable, pageNumber);
                            tables.Add(tableData);
                        }
                    }
                }
                else
                {
                    // Extract from detected regions
                    IExtractionAlgorithm extractor = new BasicExtractionAlgorithm();

                    foreach (var region in regions)
                    {
                        var area = page.GetArea(region.BoundingBox);
                        var tabulaTables = extractor.Extract(area);

                        foreach (var tabulaTable in tabulaTables)
                        {
                            if (tabulaTable.RowCount > 0)
                            {
                                var tableData = ConvertTabulaTableToTableData(tabulaTable, pageNumber);
                                tables.Add(tableData);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtilities.LogInfo("TabulaTableExtractorService.TryStreamExtraction", $"Stream extraction failed: {ex.Message}");
            }

            return tables;
        }

        /// <summary>
        /// Converts a Tabula Table object to our TableData format.
        /// </summary>
        private TableData ConvertTabulaTableToTableData(Table tabulaTable, int pageNumber)
        {
            var rows = new List<List<string>>();

            LogUtilities.LogInfo("TabulaTableExtractorService.ConvertTabulaTableToTableData", 
                $"Converting Tabula table with {tabulaTable.RowCount} rows");

            int rowIndex = 0;
            foreach (var tabulaRow in tabulaTable.Rows)
            {
                var row = new List<string>();
                int cellIndex = 0;
                
                foreach (var cell in tabulaRow)
                {
                    string cellText = cell.GetText()?.Trim() ?? string.Empty;
                    row.Add(cellText);
                    
                    // Log first few rows for debugging
                    if (rowIndex < 3)
                    {
                        LogUtilities.LogInfo("TabulaTableExtractorService.ConvertTabulaTableToTableData", 
                            $"  Row {rowIndex}, Cell {cellIndex}: [{cellText}]");
                    }
                    cellIndex++;
                }
                
                rows.Add(row);
                
                if (rowIndex < 3)
                {
                    LogUtilities.LogInfo("TabulaTableExtractorService.ConvertTabulaTableToTableData", 
                        $"  Row {rowIndex} has {row.Count} cells");
                }
                
                rowIndex++;
            }

            var tableData = new TableData
            {
                PageNumber = pageNumber,
                Rows = rows
            };

            LogUtilities.LogInfo("TabulaTableExtractorService.ConvertTabulaTableToTableData", 
                $"Converted Tabula table: {tableData.RowCount} rows x {tableData.ColumnCount} columns");

            return tableData;
        }
    }
}
