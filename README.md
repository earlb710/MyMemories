# MyMemories
Windows application to keep record of files, links, documents - all in a structured form.

## Features

This WinUI 3 application provides comprehensive file viewing capabilities:

### Supported File Types

- **Images**: JPG, JPEG, PNG, GIF, BMP, ICO
- **Web Content**: HTML, HTM
- **Documents**: PDF
- **Text Files**: TXT, XML, JSON, MD, LOG, CS, XAML, CONFIG, INI, YAML, YML, CSV

### Key Capabilities

1. **Image Viewing**: Display images with proper scaling and quality
2. **HTML Rendering**: View HTML files with full web rendering using WebView2
3. **PDF Support**: Open and view PDF documents using WebView2's native PDF viewer
4. **Text Editor**: View text-based files with syntax preservation
5. **File Information**: Display file name and size in the status bar

## Requirements

- Windows 10 version 1809 (build 17763) or higher
- Windows 11 (recommended)
- .NET 8.0 or higher
- WebView2 Runtime (usually pre-installed on Windows 11)

## Building the Application

```bash
# Restore dependencies
dotnet restore MyMemories.sln

# Build the application
dotnet build MyMemories.sln --configuration Release

# Run the application
dotnet run --project MyMemories/MyMemories.csproj
```

## Usage

1. Launch the application
2. Click the "Open File" button
3. Select a file from the file picker dialog
4. The file will be displayed in the appropriate viewer based on its type

## Architecture

The application uses:
- **WinUI 3**: Modern Windows UI framework
- **WebView2**: For rendering HTML and PDF content
- **Windows App SDK**: For Windows platform integration
- **Image controls**: For displaying image files
- **TextBlock**: For displaying text content

## Project Structure

```
MyMemories/
├── MyMemories.sln          # Solution file
├── MyMemories/
│   ├── MyMemories.csproj   # Project file
│   ├── App.xaml            # Application definition
│   ├── App.xaml.cs         # Application logic
│   ├── MainWindow.xaml     # Main window UI
│   ├── MainWindow.xaml.cs  # Main window logic
│   ├── app.manifest        # Application manifest
│   └── Assets/             # Application assets
└── README.md               # This file
```
