using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MyMemories;

public sealed partial class MainWindow
{
    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var openPicker = new FileOpenPicker();
            var hWnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(openPicker, hWnd);

            openPicker.FileTypeFilter.Add("*");
            
            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                await LoadFileAsync(file);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error opening file: {ex.Message}";
        }
    }

    private async Task LoadFileAsync(StorageFile file, string? description = null)
    {
        try
        {
            StatusText.Text = $"Loading {file.Name}...";

            HideAllViewers();

            var result = await _fileViewerService!.LoadFileAsync(file);
            
            await _detailsViewService!.ShowFileHeaderAsync(file.Name, description, file, result.Bitmap);
            HeaderViewerScroll.Visibility = Visibility.Visible;
            
            ShowViewer(result.ViewerType);

            var properties = await file.GetBasicPropertiesAsync();
            var fileSize = FileViewerService.FormatFileSize(properties.Size);
            StatusText.Text = $"Loaded: {file.Name} ({fileSize})";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading file: {ex.Message}";
            ShowWelcome();
        }
    }
}