using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace MyMemories.Services;

/// <summary>
/// Service for displaying native Windows folder picker dialogs using COM interop.
/// </summary>
public class FolderPickerService
{
    // COM interface for IFileOpenDialog
    [ComImport]
    [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int alignment);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    private enum SIGDN : uint
    {
        FILESYSPATH = 0x80058000
    }

    [ComImport]
    [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialog
    {
    }

    private const uint FOS_PICKFOLDERS = 0x00000020;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out IShellItem ppv);

    private readonly Window _window;

    /// <summary>
    /// Initializes a new instance of the FolderPickerService.
    /// </summary>
    /// <param name="window">The parent window for the folder picker dialog.</param>
    public FolderPickerService(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    /// <summary>
    /// Opens a modern Windows folder browser dialog.
    /// </summary>
    /// <param name="startingDirectory">Optional starting directory path. If null or invalid, uses default location.</param>
    /// <param name="title">The title to display on the dialog.</param>
    /// <returns>The selected folder path, or null if cancelled or failed.</returns>
    public string? BrowseForFolder(string? startingDirectory = null, string title = "Select Folder")
    {
        try
        {
            var dialog = new FileOpenDialog() as IFileOpenDialog;
            if (dialog == null)
            {
                Debug.WriteLine("Failed to create FileOpenDialog COM object");
                return null;
            }

            try
            {
                // Set options for folder picker
                dialog.SetOptions(FOS_PICKFOLDERS);
                dialog.SetTitle(title);

                // Set starting directory if provided and valid
                if (!string.IsNullOrEmpty(startingDirectory) && Directory.Exists(startingDirectory))
                {
                    try
                    {
                        var guid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"); // IShellItem
                        SHCreateItemFromParsingName(startingDirectory, IntPtr.Zero, guid, out IShellItem item);
                        dialog.SetFolder(item);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Could not set starting directory '{startingDirectory}': {ex.Message}");
                        // Continue without setting starting directory
                    }
                }

                var hWnd = WindowNative.GetWindowHandle(_window);
                var hr = dialog.Show(hWnd);

                if (hr == 0) // S_OK
                {
                    dialog.GetResult(out IShellItem result);
                    result.GetDisplayName(SIGDN.FILESYSPATH, out string path);
                    return path;
                }
                else if (hr == unchecked((int)0x800704C7)) // ERROR_CANCELLED
                {
                    Debug.WriteLine("User cancelled folder selection");
                    return null;
                }
                else
                {
                    Debug.WriteLine($"Folder picker dialog returned HRESULT: 0x{hr:X8}");
                    return null;
                }
            }
            finally
            {
                Marshal.ReleaseComObject(dialog);
            }
        }
        catch (COMException comEx)
        {
            Debug.WriteLine($"COM exception in BrowseForFolder: {comEx.Message} (HRESULT: 0x{comEx.HResult:X8})");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in BrowseForFolder: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Opens a modern Windows folder browser dialog with validation.
    /// </summary>
    /// <param name="startingDirectory">Optional starting directory path.</param>
    /// <param name="title">The title to display on the dialog.</param>
    /// <param name="mustExist">If true, only allows selecting existing folders.</param>
    /// <returns>The selected folder path, or null if cancelled or failed.</returns>
    public string? BrowseForFolderWithValidation(string? startingDirectory = null, string title = "Select Folder", bool mustExist = true)
    {
        var selectedPath = BrowseForFolder(startingDirectory, title);
        
        if (string.IsNullOrEmpty(selectedPath))
            return null;

        if (mustExist && !Directory.Exists(selectedPath))
        {
            Debug.WriteLine($"Selected path does not exist: {selectedPath}");
            return null;
        }

        return selectedPath;
    }
}