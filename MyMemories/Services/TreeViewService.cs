using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;

namespace MyMemories.Services;

/// <summary>
/// Service for managing TreeView operations.
/// </summary>
public class TreeViewService
{
    private readonly TreeView _treeView;

    public TreeViewService(TreeView treeView)
    {
        _treeView = treeView;
    }

    /// <summary>
    /// Inserts a category node at the correct position (after all existing categories).
    /// </summary>
    public void InsertCategoryNode(TreeViewNode categoryNode)
    {
        int insertIndex = 0;
        foreach (var node in _treeView.RootNodes)
        {
            if (node.Content is CategoryItem)
            {
                insertIndex++;
            }
            else
            {
                break;
            }
        }
        _treeView.RootNodes.Insert(insertIndex, categoryNode);
    }

    /// <summary>
    /// Inserts a subcategory node under a parent category node.
    /// </summary>
    public void InsertSubCategoryNode(TreeViewNode parentNode, TreeViewNode subCategoryNode)
    {
        // Insert subcategory at the beginning (before any links)
        int insertIndex = 0;
        foreach (var child in parentNode.Children)
        {
            if (child.Content is CategoryItem)
            {
                insertIndex++;
            }
            else
            {
                break;
            }
        }
        parentNode.Children.Insert(insertIndex, subCategoryNode);
        parentNode.IsExpanded = true;
    }

    /// <summary>
    /// Gets the parent category node for a given node.
    /// </summary>
    public TreeViewNode? GetParentCategoryNode(TreeViewNode? node)
    {
        if (node == null) return null;
        
        if (node.Content is CategoryItem)
            return node;
        
        return node.Parent;
    }

    /// <summary>
    /// Gets the full category path (e.g., "Category1.SubCat1.SubSub2") for a given node.
    /// </summary>
    public string GetCategoryPath(TreeViewNode? node)
    {
        if (node == null || node.Content is not CategoryItem)
            return string.Empty;

        var pathParts = new List<string>();
        var currentNode = node;

        while (currentNode != null && currentNode.Content is CategoryItem category)
        {
            pathParts.Insert(0, category.Name);
            currentNode = currentNode.Parent;
        }

        return string.Join(".", pathParts);
    }

    /// <summary>
    /// Gets all subcategories recursively under a parent category, including the parent itself.
    /// Returns a list of CategoryNode with full hierarchical paths.
    /// </summary>
    public List<CategoryNode> GetCategoryWithSubcategories(TreeViewNode categoryNode)
    {
        var result = new List<CategoryNode>();
        
        if (categoryNode.Content is not CategoryItem)
            return result;

        // Add self first
        result.Add(new CategoryNode
        {
            Name = GetCategoryPath(categoryNode),
            Node = categoryNode
        });

        // Recursively add all subcategories
        CollectSubcategories(categoryNode, result);

        return result;
    }

    private void CollectSubcategories(TreeViewNode parentNode, List<CategoryNode> result)
    {
        foreach (var child in parentNode.Children)
        {
            if (child.Content is CategoryItem)
            {
                result.Add(new CategoryNode
                {
                    Name = GetCategoryPath(child),
                    Node = child
                });

                // Recursively collect from this subcategory
                CollectSubcategories(child, result);
            }
        }
    }

    /// <summary>
    /// Refreshes a category node by recreating it while preserving state.
    /// </summary>
    public TreeViewNode RefreshCategoryNode(TreeViewNode oldNode, CategoryItem updatedCategory)
    {
        // Store children and expansion state
        var children = oldNode.Children.ToList();
        bool wasExpanded = oldNode.IsExpanded;
        
        // Determine if this is a root node or child node
        bool isRootNode = oldNode.Parent == null;
        int nodeIndex;
        
        if (isRootNode)
        {
            nodeIndex = _treeView.RootNodes.IndexOf(oldNode);
            _treeView.RootNodes.Remove(oldNode);
        }
        else
        {
            var parentNode = oldNode.Parent!;
            nodeIndex = parentNode.Children.IndexOf(oldNode);
            parentNode.Children.Remove(oldNode);
        }

        // Create new node
        var newNode = new TreeViewNode
        {
            Content = updatedCategory,
            IsExpanded = wasExpanded
        };

        // Restore children
        foreach (var child in children)
        {
            newNode.Children.Add(child);
        }

        // Insert at same position
        if (isRootNode)
        {
            _treeView.RootNodes.Insert(nodeIndex, newNode);
        }
        else
        {
            oldNode.Parent!.Children.Insert(nodeIndex, newNode);
        }

        return newNode;
    }

    /// <summary>
    /// Refreshes a link node's content.
    /// </summary>
    public void RefreshLinkNode(TreeViewNode node, LinkItem updatedLink)
    {
        node.Content = null;
        node.Content = updatedLink;
    }
}