using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

public sealed partial class MainWindow
{
    // Store original positions for potential undo of invalid drops
    private Dictionary<TreeViewNode, (TreeViewNode? Parent, int Index)> _dragOriginalPositions = new();

    /// <summary>
    /// Handles the start of a drag operation to validate which items can be dragged.
    /// Root categories and catalog entries cannot be dragged.
    /// </summary>
    private void LinksTreeView_DragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs args)
    {
        _dragOriginalPositions.Clear();

        foreach (var item in args.Items.ToList())
        {
            if (item is TreeViewNode node)
            {
                // Prevent dragging root categories (check if node is in RootNodes collection)
                if (node.Content is CategoryItem && LinksTreeView.RootNodes.Contains(node))
                {
                    args.Cancel = true;
                    StatusText.Text = "Root categories cannot be moved";
                    return;
                }

                // Prevent dragging catalog entries (they are read-only)
                if (node.Content is LinkItem link && link.IsCatalogEntry)
                {
                    args.Cancel = true;
                    StatusText.Text = "Catalog entries cannot be moved";
                    return;
                }

                // Store original position for potential undo and audit logging
                var parent = node.Parent;
                int index = parent?.Children.IndexOf(node) ?? LinksTreeView.RootNodes.IndexOf(node);
                _dragOriginalPositions[node] = (parent, index);
            }
        }
    }

    /// <summary>
    /// Handles the completion of a drag-and-drop operation in the TreeView.
    /// Validates the final positions and saves all affected categories to persist the new tree structure.
    /// </summary>
    private async void LinksTreeView_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs args)
    {
        if (args.Items.Count == 0)
        {
            _dragOriginalPositions.Clear();
            return;
        }

        // Check if the drop was cancelled
        if (args.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.None)
        {
            _dragOriginalPositions.Clear();
            StatusText.Text = "Move cancelled";
            return;
        }

        try
        {
            // Validate that no items were dropped onto invalid targets (LinkItems)
            bool hasInvalidMove = false;
            string invalidMoveMessage = string.Empty;
            var nodesToRestore = new List<TreeViewNode>();

            foreach (var item in args.Items)
            {
                if (item is TreeViewNode node)
                {
                    // Check if the node was dropped onto a LinkItem (which is invalid)
                    // Links (URLs/files) cannot have children - only categories can
                    if (node.Parent?.Content is LinkItem)
                    {
                        hasInvalidMove = true;
                        invalidMoveMessage = "Items cannot be placed inside URL/file links - only categories can have children";
                        nodesToRestore.Add(node);
                    }
                }
            }

            // If invalid move detected, restore original positions
            if (hasInvalidMove)
            {
                foreach (var node in nodesToRestore)
                {
                    if (_dragOriginalPositions.TryGetValue(node, out var original))
                    {
                        // Remove from current (invalid) parent
                        if (node.Parent != null)
                        {
                            node.Parent.Children.Remove(node);
                        }
                        else if (LinksTreeView.RootNodes.Contains(node))
                        {
                            LinksTreeView.RootNodes.Remove(node);
                        }

                        // Restore to original position
                        if (original.Parent != null)
                        {
                            int insertIndex = Math.Min(original.Index, original.Parent.Children.Count);
                            original.Parent.Children.Insert(insertIndex, node);
                        }
                        else
                        {
                            int insertIndex = Math.Min(original.Index, LinksTreeView.RootNodes.Count);
                            LinksTreeView.RootNodes.Insert(insertIndex, node);
                        }
                    }
                }

                _dragOriginalPositions.Clear();
                StatusText.Text = $"Invalid move: {invalidMoveMessage}";
                return;
            }

            // Collect all root categories that need to be saved and audit log entries
            var affectedRootCategories = new HashSet<TreeViewNode>();
            var auditLogEntries = new List<(string categoryName, string itemType, string itemName, string fromPath, string toPath)>();

            foreach (var item in args.Items)
            {
                if (item is TreeViewNode node)
                {
                    // Capture original path for audit logging before updating
                    string originalPath = string.Empty;
                    TreeViewNode? originalRootCategory = null;
                    
                    if (_dragOriginalPositions.TryGetValue(node, out var original) && original.Parent != null)
                    {
                        originalPath = _treeViewService!.GetCategoryPath(original.Parent);
                        originalRootCategory = GetRootCategoryNode(original.Parent);
                        
                        if (originalRootCategory != null && !affectedRootCategories.Contains(originalRootCategory))
                        {
                            affectedRootCategories.Add(originalRootCategory);
                        }
                    }

                    // Find the root category for this node
                    var rootCategory = GetRootCategoryNode(node);
                    affectedRootCategories.Add(rootCategory);

                    // If the node was moved to a new parent, we need to update its CategoryPath
                    UpdateNodeCategoryPaths(node);
                    
                    // Get new path after update
                    string newPath = node.Parent != null ? _treeViewService!.GetCategoryPath(node.Parent) : "(root)";
                    
                    // Check if this is actually a move (not just reordering within the same parent)
                    if (!string.IsNullOrEmpty(originalPath) && originalPath != newPath)
                    {
                        string itemType = node.Content is CategoryItem ? "Subcategory" : "Link";
                        string itemName = node.Content is CategoryItem cat ? cat.Name : 
                                         (node.Content is LinkItem link ? link.Title : "Unknown");
                        
                        // Add audit log entry for both source and destination root categories
                        if (originalRootCategory?.Content is CategoryItem srcCat && srcCat.IsAuditLoggingEnabled)
                        {
                            auditLogEntries.Add((srcCat.Name, itemType, itemName, originalPath, newPath));
                        }
                        
                        if (originalRootCategory != rootCategory && 
                            rootCategory.Content is CategoryItem dstCat && 
                            dstCat.IsAuditLoggingEnabled)
                        {
                            auditLogEntries.Add((dstCat.Name, itemType, itemName, originalPath, newPath));
                        }
                    }
                }
            }

            // Save all affected root categories
            foreach (var rootCategory in affectedRootCategories)
            {
                if (rootCategory.Content is CategoryItem category)
                {
                    category.ModifiedDate = DateTime.Now;
                    await _categoryService!.SaveCategoryAsync(rootCategory);
                }
            }

            // Write audit log entries
            foreach (var (categoryName, itemType, itemName, fromPath, toPath) in auditLogEntries)
            {
                await _configService!.AuditLogService!.LogDragDropMoveAsync(categoryName, itemType, itemName, fromPath, toPath);
            }

            _dragOriginalPositions.Clear();
            StatusText.Text = $"Tree structure saved ({affectedRootCategories.Count} categor{(affectedRootCategories.Count == 1 ? "y" : "ies")} updated)";
        }
        catch (Exception ex)
        {
            _dragOriginalPositions.Clear();
            StatusText.Text = $"Error saving tree structure: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[LinksTreeView_DragItemsCompleted] Error: {ex}");
        }
    }

    /// <summary>
    /// Recursively updates the CategoryPath property for all LinkItem children after a drag operation.
    /// </summary>
    private void UpdateNodeCategoryPaths(TreeViewNode node)
    {
        if (node.Content is LinkItem link)
        {
            // Update the CategoryPath to reflect the new parent
            var parentCategoryNode = node.Parent;
            if (parentCategoryNode != null && parentCategoryNode.Content is CategoryItem)
            {
                link.CategoryPath = _treeViewService!.GetCategoryPath(parentCategoryNode);
                link.ModifiedDate = DateTime.Now;
            }
        }
        else if (node.Content is CategoryItem category)
        {
            // Update ModifiedDate for moved categories
            category.ModifiedDate = DateTime.Now;
        }

        // Recursively update children
        foreach (var child in node.Children)
        {
            UpdateNodeCategoryPaths(child);
        }
    }
}
