using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyMemories.Models;
using MyMemories.Services;
using MyMemories.Utilities;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MyMemories;

/// <summary>
/// Window for extracting tabular data from PDF documents.
/// Features split view with PDF viewer on left and extracted data grid on right.
/// </summary>
public sealed partial class DataExtractorWindow : Window
{
    private readonly string _pdfPath;
    private readonly PdfTableExtractorService _extractorService;
    private List<TableData> _extractedTables = new();
    private int _currentTableIndex = 0;

    public DataExtractorWindow(string pdfPath)
    {
        this.InitializeComponent();
        
        _pdfPath = pdfPath;
        _extractorService = new PdfTableExtractorService();
        
        Title = $"PDF Data Extractor - {Path.GetFileName(pdfPath)}";
        
        // Set initial window size (1000x600) - WinUI 3 requires programmatic sizing
        this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1000, 600));
        
        LoadPdfAsync();
    }

    /// <summary>
    /// Loads the PDF document into the WebView2 control.
    /// </summary>
    private async void LoadPdfAsync()
    {
        try
        {
            PdfLoadingRing.Visibility = Visibility.Visible;
            
            // Ensure WebView2 is initialized
            await PdfViewer.EnsureCoreWebView2Async();
            
            // Load PDF in WebView2
            PdfViewer.Source = new Uri(_pdfPath);
            
            LogUtilities.LogInfo("DataExtractorWindow.LoadPdfAsync", 
                $"Loaded PDF: {_pdfPath}");
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("DataExtractorWindow.LoadPdfAsync", "Error loading PDF", ex);
            StatusText.Text = "Error loading PDF document";
        }
        finally
        {
            PdfLoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Handles the Extract Tables button click.
    /// </summary>
    private async void ExtractButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ExtractButton.IsEnabled = false;
            ExtractionProgressRing.IsActive = true;
            ExtractionProgressRing.Visibility = Visibility.Visible;
            StatusText.Text = "Extracting tables from PDF...";
            
            // Extract tables from PDF
            _extractedTables = await _extractorService.ExtractTablesAsync(_pdfPath);
            
            if (_extractedTables.Count == 0)
            {
                StatusText.Text = "No tables detected in this PDF";
                EmptyStateText.Text = "No tables were found in the PDF document.\n\nThis may occur if:\n• The PDF contains no tabular data\n• Tables are images rather than text\n• The document structure is too complex";
                EmptyStateText.Visibility = Visibility.Visible;
                return;
            }
            
            // Display first table
            _currentTableIndex = 0;
            DisplayCurrentTable();
            
            // Enable export/copy buttons
            ExportButton.IsEnabled = true;
            CopyButton.IsEnabled = true;
            
            // Enable navigation buttons if multiple tables
            UpdateNavigationButtons();
            
            StatusText.Text = $"Successfully extracted {_extractedTables.Count} table(s)";
            LogUtilities.LogInfo("DataExtractorWindow.ExtractButton_Click", 
                $"Extracted {_extractedTables.Count} tables");
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("DataExtractorWindow.ExtractButton_Click", "Error extracting tables", ex);
            StatusText.Text = "Error extracting tables from PDF";
        }
        finally
        {
            ExtractButton.IsEnabled = true;
            ExtractionProgressRing.IsActive = false;
            ExtractionProgressRing.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Updates the enabled state of navigation buttons based on current table index.
    /// </summary>
    private void UpdateNavigationButtons()
    {
        if (_extractedTables.Count <= 1)
        {
            PreviousTableButton.IsEnabled = false;
            NextTableButton.IsEnabled = false;
        }
        else
        {
            PreviousTableButton.IsEnabled = _currentTableIndex > 0;
            NextTableButton.IsEnabled = _currentTableIndex < _extractedTables.Count - 1;
        }
    }

    /// <summary>
    /// Navigates to the previous table.
    /// </summary>
    private void PreviousTableButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTableIndex > 0)
        {
            _currentTableIndex--;
            DisplayCurrentTable();
            UpdateNavigationButtons();
        }
    }

    /// <summary>
    /// Navigates to the next table.
    /// </summary>
    private void NextTableButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTableIndex < _extractedTables.Count - 1)
        {
            _currentTableIndex++;
            DisplayCurrentTable();
            UpdateNavigationButtons();
        }
    }

    /// <summary>
    /// Displays the current table in the data grid.
    /// </summary>
    private void DisplayCurrentTable()
    {
        if (_extractedTables.Count == 0 || _currentTableIndex >= _extractedTables.Count)
            return;

        var table = _extractedTables[_currentTableIndex];
        
        // Hide empty state text
        EmptyStateText.Visibility = Visibility.Collapsed;
        
        // Clear existing content
        DataGridContainer.Children.Clear();
        
        // Create a grid to display the table
        var displayGrid = new Grid
        {
            Margin = new Thickness(0)
        };
        
        // Add column definitions
        for (int i = 0; i < table.ColumnCount; i++)
        {
            displayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 100 });
        }
        
        // Add row definitions
        for (int i = 0; i < table.RowCount; i++)
        {
            displayGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        
        // Populate grid with data
        for (int rowIndex = 0; rowIndex < table.RowCount; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            
            for (int colIndex = 0; colIndex < row.Count; colIndex++)
            {
                var cellBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8),
                    Background = rowIndex == 0 
                        ? new SolidColorBrush(Microsoft.UI.Colors.WhiteSmoke) 
                        : (rowIndex % 2 == 0 
                            ? new SolidColorBrush(Microsoft.UI.Colors.White) 
                            : new SolidColorBrush(Microsoft.UI.Colors.AliceBlue))
                };
                
                var textBlock = new TextBlock
                {
                    Text = row[colIndex],
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                    FontWeight = rowIndex == 0 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal
                };
                
                cellBorder.Child = textBlock;
                Grid.SetRow(cellBorder, rowIndex);
                Grid.SetColumn(cellBorder, colIndex);
                displayGrid.Children.Add(cellBorder);
            }
        }
        
        DataGridContainer.Children.Add(displayGrid);
        
        // Update status with table info
        if (_extractedTables.Count > 1)
        {
            StatusText.Text = $"Showing table {_currentTableIndex + 1} of {_extractedTables.Count} (Page {table.PageNumber}) - {table.RowCount} rows × {table.ColumnCount} columns";
        }
        else
        {
            StatusText.Text = $"Showing table from page {table.PageNumber} - {table.RowCount} rows × {table.ColumnCount} columns";
        }
    }

    /// <summary>
    /// Handles the Export to CSV button click.
    /// </summary>
    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_extractedTables.Count == 0)
            return;

        try
        {
            // Create file save picker
            var savePicker = new FileSavePicker();
            InitializeWithWindow.Initialize(savePicker, WindowNative.GetWindowHandle(this));
            
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("CSV Files", new List<string> { ".csv" });
            savePicker.SuggestedFileName = $"{Path.GetFileNameWithoutExtension(_pdfPath)}_table{_currentTableIndex + 1}";
            
            var file = await savePicker.PickSaveFileAsync();
            
            if (file != null)
            {
                var table = _extractedTables[_currentTableIndex];
                await _extractorService.ExportToCsvAsync(table, file.Path);
                
                StatusText.Text = $"Table exported successfully to {file.Name}";
                LogUtilities.LogInfo("DataExtractorWindow.ExportButton_Click", 
                    $"Exported table to: {file.Path}");
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("DataExtractorWindow.ExportButton_Click", "Error exporting to CSV", ex);
            StatusText.Text = "Error exporting table to CSV";
        }
    }

    /// <summary>
    /// Handles the Copy to Clipboard button click.
    /// </summary>
    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_extractedTables.Count == 0)
            return;

        try
        {
            var table = _extractedTables[_currentTableIndex];
            _extractorService.CopyToClipboard(table);
            
            StatusText.Text = "Table copied to clipboard";
            LogUtilities.LogInfo("DataExtractorWindow.CopyButton_Click", 
                "Copied table to clipboard");
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("DataExtractorWindow.CopyButton_Click", "Error copying to clipboard", ex);
            StatusText.Text = "Error copying table to clipboard";
        }
    }
}
