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
    
    /// <summary>
    /// Empty template used for nodes with invalid content to minimize visual impact.
    /// This template will be created on first use.
    /// </summary>
    private static DataTemplate? _emptyTemplate;

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        // TreeView passes TreeViewNode, not the content directly - extract it
        object? actualItem = item;
        if (item is TreeViewNode node)
        {
            actualItem = node.Content;
        }
        
        // For nodes with null content, return an empty template to minimize visual impact
        // The node will still render but with minimal height
        if (actualItem == null)
        {
            return GetEmptyTemplate();
        }
        
        if (actualItem is CategoryItem category)
        {
            // Additional validation - ensure the category has a valid name
            if (string.IsNullOrEmpty(category.Name))
            {
                return GetEmptyTemplate();
            }
            return CategoryTemplate;
        }
        else if (actualItem is LinkItem link)
        {
            // Additional validation - ensure the link has a valid title
            if (string.IsNullOrEmpty(link.Title))
            {
                return GetEmptyTemplate();
            }
            return LinkTemplate;
        }
        
        // For unknown content types, return empty template
        return GetEmptyTemplate();
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
    
    /// <summary>
    /// Gets or creates an empty template that renders with minimal visual impact.
    /// </summary>
    private static DataTemplate GetEmptyTemplate()
    {
        if (_emptyTemplate == null)
        {
            // Create a template with zero height to effectively hide invalid nodes
            _emptyTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
                @"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
                    <Border Height=""0"" Width=""0"" Visibility=""Collapsed""/>
                </DataTemplate>");
        }
        return _emptyTemplate;
    }
}