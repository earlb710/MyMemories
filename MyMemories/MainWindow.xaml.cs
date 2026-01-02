using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MyMemories;

/// <summary>
/// Main window for the MyMemories file viewer application.
/// </summary>
public sealed partial class MainWindow : Window
{
    private string? _currentFilePath;

    public MainWindow()
    {
        this.InitializeComponent();
        Title = "My Memories - File Viewer";
        
        // Initialize WebView2 asynchronously
        _ = InitializeWebView();
    }

    private async Task InitializeWebView()
    {
        try
        {
            await WebViewer.EnsureCoreWebView2Async();
            StatusText.Text = "Ready";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"WebView2 initialization warning: {ex.Message}";
        }
    }

    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Create a file picker
            var openPicker = new FileOpenPicker();
            
            // Initialize the file picker with the window handle
            var hWnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(openPicker, hWnd);

            // Set file type filters
            openPicker.FileTypeFilter.Add("*"); // All files
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".png");
            openPicker.FileTypeFilter.Add(".gif");
            openPicker.FileTypeFilter.Add(".bmp");
            openPicker.FileTypeFilter.Add(".html");
            openPicker.FileTypeFilter.Add(".htm");
            openPicker.FileTypeFilter.Add(".pdf");
            openPicker.FileTypeFilter.Add(".txt");
            openPicker.FileTypeFilter.Add(".xml");
            openPicker.FileTypeFilter.Add(".json");
            openPicker.FileTypeFilter.Add(".md");
            openPicker.FileTypeFilter.Add(".log");

            // Pick a file
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                await LoadFile(file);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error opening file: {ex.Message}";
        }
    }

    private async Task LoadFile(StorageFile file)
    {
        try
        {
            _currentFilePath = file.Path;
            CurrentFileText.Text = file.Name;
            StatusText.Text = $"Loading {file.Name}...";

            // Hide all viewers
            HideAllViewers();

            string extension = file.FileType.ToLowerInvariant();

            // Determine file type and display accordingly
            if (IsImageFile(extension))
            {
                await LoadImage(file);
            }
            else if (extension == ".html" || extension == ".htm")
            {
                await LoadHtml(file);
            }
            else if (extension == ".pdf")
            {
                await LoadPdf(file);
            }
            else if (IsTextFile(extension))
            {
                await LoadText(file);
            }
            else
            {
                // Try to load as text for unknown types
                await LoadText(file);
            }

            StatusText.Text = $"Loaded: {file.Name} ({FormatFileSize(await GetFileSize(file))})";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading file: {ex.Message}";
            ShowWelcome();
        }
    }

    private void HideAllViewers()
    {
        ImageViewer.Visibility = Visibility.Collapsed;
        WebViewer.Visibility = Visibility.Collapsed;
        TextViewer.Visibility = Visibility.Collapsed;
        WelcomePanel.Visibility = Visibility.Collapsed;
    }

    private void ShowWelcome()
    {
        HideAllViewers();
        WelcomePanel.Visibility = Visibility.Visible;
    }

    private bool IsImageFile(string extension)
    {
        return extension == ".jpg" || extension == ".jpeg" || 
               extension == ".png" || extension == ".gif" || 
               extension == ".bmp" || extension == ".ico";
    }

    private bool IsTextFile(string extension)
    {
        return extension == ".txt" || extension == ".xml" || 
               extension == ".json" || extension == ".md" || 
               extension == ".log" || extension == ".cs" || 
               extension == ".xaml" || extension == ".config" ||
               extension == ".ini" || extension == ".yaml" ||
               extension == ".yml" || extension == ".csv";
    }

    private async Task LoadImage(StorageFile file)
    {
        try
        {
            using var stream = await file.OpenReadAsync();
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            
            ImageViewer.Source = bitmap;
            ImageViewer.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading image: {ex.Message}";
            throw;
        }
    }

    private async Task LoadHtml(StorageFile file)
    {
        try
        {
            // Ensure WebView2 is initialized
            if (WebViewer.CoreWebView2 == null)
            {
                await WebViewer.EnsureCoreWebView2Async();
            }

            // Load HTML file
            WebViewer.Source = new Uri(file.Path);
            WebViewer.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading HTML: {ex.Message}";
            // Fallback to text view
            await LoadText(file);
        }
    }

    private async Task LoadPdf(StorageFile file)
    {
        try
        {
            // Ensure WebView2 is initialized
            if (WebViewer.CoreWebView2 == null)
            {
                await WebViewer.EnsureCoreWebView2Async();
            }

            // Load PDF file - WebView2 can display PDFs natively
            WebViewer.Source = new Uri(file.Path);
            WebViewer.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading PDF: {ex.Message}";
            throw;
        }
    }

    private async Task LoadText(StorageFile file)
    {
        try
        {
            string content = await FileIO.ReadTextAsync(file);
            TextViewer.Text = content;
            TextViewer.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading text file: {ex.Message}";
            throw;
        }
    }

    private async Task<ulong> GetFileSize(StorageFile file)
    {
        try
        {
            var properties = await file.GetBasicPropertiesAsync();
            return properties.Size;
        }
        catch
        {
            return 0;
        }
    }

    private string FormatFileSize(ulong bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
