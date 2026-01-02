using Microsoft.UI.Xaml;

namespace MyMemories;

/// <summary>
/// Window that hosts the TreeView page with split view layout.
/// </summary>
public sealed partial class TreeViewWindow : Window
{
    public TreeViewWindow()
    {
        this.InitializeComponent();
        Title = "My Memories - Tree View";
        
        // Set the page as content
        Content = new TreeViewPage();
    }
}