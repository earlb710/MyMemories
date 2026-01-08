using System;
using System.Threading.Tasks;

namespace MyMemories.Services;

public class DoubleTapHandlerService
{
    private readonly FileLauncherService _fileLauncherService;

    public DoubleTapHandlerService(FileLauncherService fileLauncherService)
    {
        _fileLauncherService = fileLauncherService;
    }

    public async Task HandleDoubleTapAsync(LinkItem linkItem, Microsoft.UI.Xaml.Controls.TreeViewNode? selectedNode, Action<string> setStatus)
    {
        // Don't do anything if URL is empty (e.g., temporary "busy creating" nodes)
        if (string.IsNullOrEmpty(linkItem.Url))
        {
            return;
        }

        // Check if this is a zip entry (URL contains "::")
        if (linkItem.Url.Contains("::"))
        {
            // If it's a directory within the zip, just expand/collapse
            if (linkItem.IsDirectory && selectedNode != null)
            {
                selectedNode.IsExpanded = !selectedNode.IsExpanded;
            }
            else
            {
                // It's a file within the zip - extract and open it
                await _fileLauncherService.OpenZipEntryAsync(linkItem, setStatus);
            }
        }
        else if (linkItem.IsDirectory && selectedNode != null)
        {
            // Expand/collapse regular directory
            selectedNode.IsExpanded = !selectedNode.IsExpanded;
        }
        else if (!string.IsNullOrEmpty(linkItem.Url))
        {
            // Open regular file
            await _fileLauncherService.OpenFileAsync(linkItem.Url, setStatus);
        }
    }
}