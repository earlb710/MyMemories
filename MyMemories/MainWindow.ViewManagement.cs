using Microsoft.UI.Xaml;
using MyMemories.Services;
using System.Collections.ObjectModel;

namespace MyMemories;

public sealed partial class MainWindow
{
    private void HideAllViewers()
    {
        ImageViewer.Visibility = Visibility.Collapsed;
        WebViewer.Visibility = Visibility.Collapsed;
        TextViewerScroll.Visibility = Visibility.Collapsed;
        DetailsViewerScroll.Visibility = Visibility.Collapsed;
        WelcomePanel.Visibility = Visibility.Collapsed;
        HeaderViewerScroll.Visibility = Visibility.Collapsed;
    }

    private void ShowWelcome()
    {
        HideAllViewers();
        WelcomePanel.Visibility = Visibility.Visible;
    }

    private void ShowViewer(FileViewerType viewerType)
    {
        switch (viewerType)
        {
            case FileViewerType.Image:
                ImageViewer.Visibility = Visibility.Visible;
                break;
            case FileViewerType.Web:
                WebViewer.Visibility = Visibility.Visible;
                break;
            case FileViewerType.Text:
                TextViewerScroll.Visibility = Visibility.Visible;
                break;
        }
    }

    private ObservableCollection<TreeViewNode> _rootItems = null!; // Add null-forgiving operator
}