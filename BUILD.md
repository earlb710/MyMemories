# My Memories - Build Instructions

## Building on Windows

This is a WinUI 3 application that must be built on Windows with Visual Studio 2022 or later.

### Prerequisites

1. **Visual Studio 2022** or later with:
   - .NET desktop development workload
   - Windows application development workload
   - Windows 10 SDK (10.0.19041.0 or later)

2. **WebView2 Runtime**: Usually pre-installed on Windows 11. For Windows 10, download from:
   https://developer.microsoft.com/microsoft-edge/webview2/

### Build Steps

#### Using Visual Studio

1. Open `MyMemories.sln` in Visual Studio 2022
2. Select the `Release` configuration and `x64` platform
3. Build > Build Solution (or press Ctrl+Shift+B)
4. Run the application with F5 or Debug > Start Debugging

#### Using Command Line

```powershell
# Restore NuGet packages
dotnet restore MyMemories.sln

# Build the project
dotnet build MyMemories.sln --configuration Release

# Or use MSBuild directly
msbuild MyMemories.sln /p:Configuration=Release /p:Platform=x64
```

### Running the Application

After building, the executable will be located at:
```
MyMemories/bin/x64/Release/net8.0-windows10.0.19041.0/MyMemories.exe
```

## Features Implemented

✅ Image viewing (JPG, PNG, GIF, BMP)
✅ HTML viewing with WebView2
✅ PDF viewing with WebView2
✅ Text file viewing
✅ File picker integration
✅ Status bar with file information
✅ Responsive UI with proper error handling

## Troubleshooting

### WebView2 Not Found

If you encounter WebView2 errors, install the WebView2 Runtime from Microsoft.

### Build Errors

- Ensure you have the Windows 10 SDK installed
- Make sure Visual Studio has the Windows App SDK components
- Try cleaning the solution: Build > Clean Solution, then rebuild

### Platform Errors

- This project targets x64 architecture only
- Ensure "x64" is selected in the platform dropdown in Visual Studio
