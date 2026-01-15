using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace MyMemories.Services.Interfaces;

/// <summary>
/// Interface for the TreeViewService that manages TreeView operations.
/// </summary>
public interface ITreeViewService
{
    /// <summary>
    /// Inserts a category node at the correct position (after all existing categories).
    /// </summary>
    void InsertCategoryNode(TreeViewNode categoryNode);

    /// <summary>
    /// Inserts a subcategory node under a parent category node.
    /// </summary>
    void InsertSubCategoryNode(TreeViewNode parentNode, TreeViewNode subCategoryNode);

    /// <summary>
    /// Gets the parent category node for a given node.
    /// </summary>
    TreeViewNode? GetParentCategoryNode(TreeViewNode? node);

    /// <summary>
    /// Gets the full category path (e.g., "Category1.SubCat1.SubSub2") for a given node.
    /// </summary>
    string GetCategoryPath(TreeViewNode? node);

    /// <summary>
    /// Gets all subcategories recursively under a parent category, including the parent itself.
    /// </summary>
    List<CategoryNode> GetCategoryWithSubcategories(TreeViewNode categoryNode);

    /// <summary>
    /// Refreshes a category node by recreating it while preserving state.
    /// </summary>
    TreeViewNode RefreshCategoryNode(TreeViewNode oldNode, CategoryItem updatedCategory);

    /// <summary>
    /// Refreshes a link node's content by recreating the node to force TreeView update.
    /// </summary>
    TreeViewNode RefreshLinkNode(TreeViewNode oldNode, LinkItem updatedLink);

    /// <summary>
    /// Selects a node in the TreeView.
    /// </summary>
    void SelectNode(TreeViewNode node);
}
