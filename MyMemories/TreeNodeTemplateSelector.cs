using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MyMemories;

/// <summary>
/// Template selector for TreeView nodes to handle CategoryItem and LinkItem.
/// </summary>
public class TreeNodeTemplateSelector : DataTemplateSelector
{
    public DataTemplate? CategoryTemplate { get; set; }
    public DataTemplate? LinkTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        // TreeView passes TreeViewNode, not the content directly - extract it
        object? actualItem = item;
        if (item is TreeViewNode node)
        {
            actualItem = node.Content;
        }
        
        DataTemplate? result = null;
        
        if (actualItem is CategoryItem)
        {
            result = CategoryTemplate;
        }
        else if (actualItem is LinkItem)
        {
            result = LinkTemplate;
        }
        
        return result;
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
}