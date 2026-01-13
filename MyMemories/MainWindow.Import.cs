using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Models;
using MyMemories.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MyMemories;

/// <summary>
/// Category import functionality for MainWindow.
/// </summary>
public sealed partial class MainWindow
{
    private CategoryImportService? _importService;

    /// <summary>
    /// Handles the Import Category Operations menu click.
    /// </summary>
    private async void MenuFile_ImportCategoryOperations_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // File picker for JSON import file
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                FileTypeFilter = { ".json" }
            };
            
            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                StatusText.Text = "Import cancelled";
                return;
            }

            // Show confirmation dialog with preview
            var previewDialog = new ContentDialog
            {
                Title = "Import Category Operations",
                Content = $"Import operations from:\n\n{file.Name}\n\nThis will process Add, Update, and Delete operations on your categories.\n\n[!] It is recommended to backup your categories before importing.",
                PrimaryButtonText = "Import",
                SecondaryButtonText = "Preview",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var result = await previewDialog.ShowAsync();

            if (result == ContentDialogResult.None)
            {
                StatusText.Text = "Import cancelled";
                return;
            }

            if (result == ContentDialogResult.Secondary)
            {
                // Show preview
                await ShowImportPreviewAsync(file.Path);
                return;
            }

            // User clicked Import - perform the import
            await PerformImportAsync(file.Path);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Import error: {ex.Message}";
            
            await new ContentDialog
            {
                Title = "Import Error",
                Content = $"An error occurred while importing:\n\n{ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            }.ShowAsync();
        }
    }

    /// <summary>
    /// Shows a preview of the import file.
    /// </summary>
    private async Task ShowImportPreviewAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var importData = System.Text.Json.JsonSerializer.Deserialize<CategoryImportData>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (importData == null)
            {
                await new ContentDialog
                {
                    Title = "Invalid File",
                    Content = "The selected file is not a valid import file.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                }.ShowAsync();
                return;
            }

            // Build preview content
            var previewPanel = new StackPanel { Spacing = 12 };

            if (!string.IsNullOrEmpty(importData.Description))
            {
                previewPanel.Children.Add(new TextBlock
                {
                    Text = importData.Description,
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });
            }

            previewPanel.Children.Add(new TextBlock
            {
                Text = $"Version: {importData.Version}",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            });

            if (importData.ImportDate.HasValue)
            {
                previewPanel.Children.Add(new TextBlock
                {
                    Text = $"Created: {importData.ImportDate.Value:yyyy-MM-dd HH:mm:ss}",
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                });
            }

            previewPanel.Children.Add(new TextBlock
            {
                Text = $"Total Operations: {importData.Operations.Count}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 0)
            });

            // Group operations by type
            var adds = importData.Operations.Count(o => o.Operation.Equals("Add", StringComparison.OrdinalIgnoreCase));
            var updates = importData.Operations.Count(o => o.Operation.Equals("Update", StringComparison.OrdinalIgnoreCase));
            var deletes = importData.Operations.Count(o => o.Operation.Equals("Delete", StringComparison.OrdinalIgnoreCase));

            var statsPanel = new StackPanel { Spacing = 4, Margin = new Thickness(12, 0, 0, 0) };
            statsPanel.Children.Add(new TextBlock { Text = $"• Add: {adds}" });
            statsPanel.Children.Add(new TextBlock { Text = $"• Update: {updates}" });
            statsPanel.Children.Add(new TextBlock { Text = $"• Delete: {deletes}" });
            previewPanel.Children.Add(statsPanel);

            // Show first few operations
            previewPanel.Children.Add(new TextBlock
            {
                Text = "Operations:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 0)
            });

            var opListPanel = new StackPanel { Spacing = 4, Margin = new Thickness(12, 0, 0, 0) };
            var opsToShow = importData.Operations.Take(10);
            
            foreach (var op in opsToShow)
            {
                var opText = $"{op.Operation} {op.Target}";
                if (op.Identifier?.CategoryPath != null)
                    opText += $" in '{op.Identifier.CategoryPath}'";
                else if (op.Identifier?.Name != null)
                    opText += $" '{op.Identifier.Name}'";

                opListPanel.Children.Add(new TextBlock 
                { 
                    Text = $"• {opText}",
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            if (importData.Operations.Count > 10)
            {
                opListPanel.Children.Add(new TextBlock
                {
                    Text = $"... and {importData.Operations.Count - 10} more",
                    FontStyle = Windows.UI.Text.FontStyle.Italic,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                });
            }

            previewPanel.Children.Add(opListPanel);

            var previewDialog = new ContentDialog
            {
                Title = "Import Preview",
                Content = new ScrollViewer
                {
                    Content = previewPanel,
                    MaxHeight = 500
                },
                PrimaryButtonText = "Import",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };

            var dialogResult = await previewDialog.ShowAsync();
            
            if (dialogResult == ContentDialogResult.Primary)
            {
                // Perform the actual import using the already-selected file
                await PerformImportAsync(filePath);
            }
        }
        catch (Exception ex)
        {
            await new ContentDialog
            {
                Title = "Preview Error",
                Content = $"Could not preview the import file:\n\n{ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            }.ShowAsync();
        }
    }

    /// <summary>
    /// Performs the actual import operation for a given file path.
    /// </summary>
    private async Task PerformImportAsync(string filePath)
    {
        try
        {
            // Show progress dialog
            var progressDialog = new ContentDialog
            {
                Title = "Importing...",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new ProgressRing { IsActive = true, Width = 50, Height = 50 },
                        new TextBlock 
                        { 
                            Text = "Processing import operations...",
                            TextWrapping = TextWrapping.Wrap,
                            TextAlignment = TextAlignment.Center
                        }
                    }
                },
                XamlRoot = Content.XamlRoot
            };

            // Show the dialog asynchronously but don't await it
            var dialogTask = progressDialog.ShowAsync();

            // Initialize import service
            _importService ??= new CategoryImportService(
                _categoryService!,
                _treeViewService!,
                LinksTreeView,
                _tagService,
                _ratingService);

            // Perform import
            StatusText.Text = "Importing category operations...";
            var importResult = await _importService.ImportFromFileAsync(filePath);

            // Hide progress dialog
            progressDialog.Hide();

            // Show results dialog
            await ShowImportResultsAsync(importResult);

            // Refresh display if any categories were modified
            if (importResult.CategoriesModified.Count > 0)
            {
                StatusText.Text = $"Import completed: {importResult.Successful} successful, {importResult.Failed} failed, {importResult.Skipped} skipped";
            }
            else
            {
                StatusText.Text = $"Import completed: {importResult.TotalOperations} operations processed";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Import error: {ex.Message}";
            
            await new ContentDialog
            {
                Title = "Import Error",
                Content = $"An error occurred while importing:\n\n{ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            }.ShowAsync();
        }
    }

    /// <summary>
    /// Shows the results of an import operation.
    /// </summary>
    private async Task ShowImportResultsAsync(ImportResult result)
    {
        var resultsPanel = new StackPanel { Spacing = 12 };

        // Summary (use Unicode symbols that render better)
        var summaryText = result.Success
            ? "[SUCCESS] Import completed successfully!"
            : "[WARNING] Import completed with errors.";

        resultsPanel.Children.Add(new TextBlock
        {
            Text = summaryText,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        // Statistics
        var statsPanel = new StackPanel { Spacing = 4 };
        statsPanel.Children.Add(new TextBlock { Text = $"Total Operations: {result.TotalOperations}" });
        statsPanel.Children.Add(new TextBlock 
        { 
            Text = $"[OK] Successful: {result.Successful}",
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green)
        });
        
        if (result.Failed > 0)
        {
            statsPanel.Children.Add(new TextBlock 
            { 
                Text = $"[X] Failed: {result.Failed}",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red)
            });
        }

        if (result.Skipped > 0)
        {
            statsPanel.Children.Add(new TextBlock 
            { 
                Text = $"[!] Skipped: {result.Skipped}",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange)
            });
        }

        statsPanel.Children.Add(new TextBlock 
        { 
            Text = $"Duration: {result.ImportDuration.TotalSeconds:F2} seconds"
        });

        resultsPanel.Children.Add(statsPanel);

        // Modified categories
        if (result.CategoriesModified.Count > 0)
        {
            resultsPanel.Children.Add(new TextBlock
            {
                Text = "Modified Categories:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 0)
            });

            var catPanel = new StackPanel { Spacing = 2, Margin = new Thickness(12, 0, 0, 0) };
            foreach (var cat in result.CategoriesModified.Take(10))
            {
                catPanel.Children.Add(new TextBlock { Text = $"  - {cat}" });
            }

            if (result.CategoriesModified.Count > 10)
            {
                catPanel.Children.Add(new TextBlock
                {
                    Text = $"... and {result.CategoriesModified.Count - 10} more",
                    FontStyle = Windows.UI.Text.FontStyle.Italic,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                });
            }

            resultsPanel.Children.Add(catPanel);
        }

        // Show failed operations
        var failedOps = result.OperationResults.Where(r => r.Status == "Failed").ToList();
        if (failedOps.Count > 0)
        {
            resultsPanel.Children.Add(new TextBlock
            {
                Text = "Failed Operations:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
                Margin = new Thickness(0, 8, 0, 0)
            });

            var failedPanel = new StackPanel { Spacing = 4, Margin = new Thickness(12, 0, 0, 0) };
            foreach (var op in failedOps.Take(20)) // Show more failed operations
            {
                failedPanel.Children.Add(new TextBlock
                {
                    Text = $"  - {op.Operation} {op.Target}: {op.Message}",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red)
                });
            }

            if (failedOps.Count > 20)
            {
                failedPanel.Children.Add(new TextBlock
                {
                    Text = $"... and {failedOps.Count - 20} more failures",
                    FontStyle = Windows.UI.Text.FontStyle.Italic,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                });
            }

            resultsPanel.Children.Add(failedPanel);
            
            // Add Copy Error Messages button
            var copyButton = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE8C8", FontSize = 14 }, // Copy icon
                        new TextBlock { Text = "Copy Error Messages", VerticalAlignment = VerticalAlignment.Center }
                    }
                },
                Margin = new Thickness(0, 8, 0, 0)
            };
            
            copyButton.Click += (s, e) =>
            {
                try
                {
                    var errorText = new System.Text.StringBuilder();
                    errorText.AppendLine($"Import Errors ({failedOps.Count} total):");
                    errorText.AppendLine($"=====================================");
                    errorText.AppendLine();
                    
                    foreach (var op in failedOps)
                    {
                        errorText.AppendLine($"Operation: {op.Operation} {op.Target}");
                        if (op.Identifier?.CategoryPath != null)
                        {
                            errorText.AppendLine($"  Path: {op.Identifier.CategoryPath}");
                        }
                        if (op.Identifier?.Title != null)
                        {
                            errorText.AppendLine($"  Title: {op.Identifier.Title}");
                        }
                        errorText.AppendLine($"  Error: {op.Message}");
                        errorText.AppendLine();
                    }
                    
                    // Clipboard access should already be on UI thread from button click,
                    // but use TryEnqueue to be safe
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                            dataPackage.SetText(errorText.ToString());
                            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                            StatusText.Text = $"Copied {failedOps.Count} error message(s) to clipboard";
                        }
                        catch (Exception clipEx)
                        {
                            StatusText.Text = $"Failed to copy: {clipEx.Message}";
                        }
                    });
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Failed to prepare error text: {ex.Message}";
                }
            };
            
            resultsPanel.Children.Add(copyButton);
        }

        var dialog = new ContentDialog
        {
            Title = "Import Results",
            Content = new ScrollViewer
            {
                Content = resultsPanel,
                MaxHeight = 600
            },
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        
        await dialog.ShowAsync();
    }
}
