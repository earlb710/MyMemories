# My Memories Sample File

This is a sample Markdown file to demonstrate the text viewing capabilities of the My Memories application.

## Features Demonstrated

When viewing this file in My Memories, you'll see:

- Plain text rendering
- Markdown syntax preserved
- Code block formatting
- List structures

## Supported File Types

### Images
- JPG/JPEG
- PNG
- GIF
- BMP
- ICO

### Web Content
- HTML
- HTM

### Documents
- PDF

### Text Files
- TXT
- XML
- JSON
- MD (Markdown)
- LOG
- CS (C# source)
- XAML
- CONFIG
- INI
- YAML/YML
- CSV

## Code Example

```csharp
public class FileViewer
{
    public async Task LoadFile(StorageFile file)
    {
        string extension = file.FileType.ToLowerInvariant();
        
        if (IsImageFile(extension))
        {
            await LoadImage(file);
        }
        else if (extension == ".html")
        {
            await LoadHtml(file);
        }
        else if (extension == ".pdf")
        {
            await LoadPdf(file);
        }
    }
}
```

## Application Architecture

The application uses:
1. **WinUI 3** for the modern Windows UI
2. **WebView2** for HTML and PDF rendering
3. **Image controls** for image display
4. **TextBlock** for text content

## Getting Started

1. Build the project in Visual Studio 2022
2. Run the application
3. Click "Open File" to select a file
4. The appropriate viewer will be displayed

---

*This file demonstrates the text viewing capability of My Memories*
