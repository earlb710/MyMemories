using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyMemories.Utilities;

/// <summary>
/// Utility methods for traversing and querying TreeView node structures.
/// Centralizes common traversal patterns used throughout the application.
/// </summary>
public static class TreeViewTraversalUtilities
{
    /// <summary>
    /// Traverses all nodes in depth-first order, executing an action on each node.
    /// </summary>
    public static void TraverseDepthFirst(TreeViewNode root, Action<TreeViewNode> action)
    {
        if (root == null) return;

        action(root);

        foreach (var child in root.Children)
        {
            TraverseDepthFirst(child, action);
        }
    }

    /// <summary>
    /// Traverses all nodes from a collection in depth-first order.
    /// </summary>
    public static void TraverseDepthFirst(IEnumerable<TreeViewNode> roots, Action<TreeViewNode> action)
    {
        foreach (var root in roots)
        {
            TraverseDepthFirst(root, action);
        }
    }

    /// <summary>
    /// Traverses all nodes in breadth-first order, executing an action on each node.
    /// </summary>
    public static void TraverseBreadthFirst(TreeViewNode root, Action<TreeViewNode> action)
    {
        if (root == null) return;

        var queue = new Queue<TreeViewNode>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            action(node);

            foreach (var child in node.Children)
            {
                queue.Enqueue(child);
            }
        }
    }

    /// <summary>
    /// Finds the first node that matches the predicate using depth-first search.
    /// </summary>
    public static TreeViewNode? FindNode(TreeViewNode root, Func<TreeViewNode, bool> predicate)
    {
        if (root == null) return null;

        if (predicate(root))
            return root;

        foreach (var child in root.Children)
        {
            var found = FindNode(child, predicate);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Finds the first node from a collection that matches the predicate.
    /// </summary>
    public static TreeViewNode? FindNode(IEnumerable<TreeViewNode> roots, Func<TreeViewNode, bool> predicate)
    {
        foreach (var root in roots)
        {
            var found = FindNode(root, predicate);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Finds all nodes that match the predicate.
    /// </summary>
    public static List<TreeViewNode> FindAllNodes(TreeViewNode root, Func<TreeViewNode, bool> predicate)
    {
        var results = new List<TreeViewNode>();
        CollectNodesRecursive(root, predicate, results);
        return results;
    }

    /// <summary>
    /// Finds all nodes from a collection that match the predicate.
    /// </summary>
    public static List<TreeViewNode> FindAllNodes(IEnumerable<TreeViewNode> roots, Func<TreeViewNode, bool> predicate)
    {
        var results = new List<TreeViewNode>();
        foreach (var root in roots)
        {
            CollectNodesRecursive(root, predicate, results);
        }
        return results;
    }

    /// <summary>
    /// Helper method to recursively collect nodes matching a predicate.
    /// </summary>
    private static void CollectNodesRecursive(TreeViewNode node, Func<TreeViewNode, bool> predicate, List<TreeViewNode> results)
    {
        if (node == null) return;

        if (predicate(node))
            results.Add(node);

        foreach (var child in node.Children)
        {
            CollectNodesRecursive(child, predicate, results);
        }
    }

    /// <summary>
    /// Gets all ancestor nodes from the given node up to the root.
    /// </summary>
    public static List<TreeViewNode> GetAncestors(TreeViewNode node)
    {
        var ancestors = new List<TreeViewNode>();
        var current = node?.Parent;

        while (current != null)
        {
            ancestors.Add(current);
            current = current.Parent;
        }

        return ancestors;
    }

    /// <summary>
    /// Gets all descendant nodes from the given node.
    /// </summary>
    public static List<TreeViewNode> GetDescendants(TreeViewNode root)
    {
        var descendants = new List<TreeViewNode>();
        
        if (root == null) return descendants;

        foreach (var child in root.Children)
        {
            descendants.Add(child);
            descendants.AddRange(GetDescendants(child));
        }

        return descendants;
    }

    /// <summary>
    /// Checks if a node contains another node as a descendant.
    /// </summary>
    public static bool ContainsDescendant(TreeViewNode ancestor, TreeViewNode target)
    {
        if (ancestor == null || target == null) return false;
        if (ancestor == target) return true;

        foreach (var child in ancestor.Children)
        {
            if (ContainsDescendant(child, target))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the depth (level) of a node in the tree (root = 0).
    /// </summary>
    public static int GetDepth(TreeViewNode node)
    {
        int depth = 0;
        var current = node?.Parent;

        while (current != null)
        {
            depth++;
            current = current.Parent;
        }

        return depth;
    }

    /// <summary>
    /// Gets the root node for a given node.
    /// </summary>
    public static TreeViewNode GetRoot(TreeViewNode node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        var current = node;
        while (current.Parent != null)
        {
            current = current.Parent;
        }

        return current;
    }

    /// <summary>
    /// Counts all nodes in a tree (including the root).
    /// </summary>
    public static int CountNodes(TreeViewNode root)
    {
        if (root == null) return 0;

        int count = 1; // Count the root
        foreach (var child in root.Children)
        {
            count += CountNodes(child);
        }

        return count;
    }

    /// <summary>
    /// Gets all leaf nodes (nodes with no children).
    /// </summary>
    public static List<TreeViewNode> GetLeafNodes(TreeViewNode root)
    {
        return FindAllNodes(root, node => node.Children.Count == 0);
    }

    /// <summary>
    /// Gets all nodes at a specific depth level.
    /// </summary>
    public static List<TreeViewNode> GetNodesAtDepth(TreeViewNode root, int targetDepth)
    {
        return FindAllNodes(root, node => GetDepth(node) == targetDepth);
    }

    /// <summary>
    /// Filters nodes by content type.
    /// </summary>
    public static List<TreeViewNode> FilterByContentType<T>(TreeViewNode root) where T : class
    {
        return FindAllNodes(root, node => node.Content is T);
    }

    /// <summary>
    /// Filters nodes by content type from a collection.
    /// </summary>
    public static List<TreeViewNode> FilterByContentType<T>(IEnumerable<TreeViewNode> roots) where T : class
    {
        return FindAllNodes(roots, node => node.Content is T);
    }

    /// <summary>
    /// Executes an action on all nodes of a specific content type.
    /// </summary>
    public static void ForEachOfType<T>(TreeViewNode root, Action<TreeViewNode, T> action) where T : class
    {
        TraverseDepthFirst(root, node =>
        {
            if (node.Content is T typedContent)
            {
                action(node, typedContent);
            }
        });
    }

    /// <summary>
    /// Executes an action on all nodes of a specific content type from a collection.
    /// </summary>
    public static void ForEachOfType<T>(IEnumerable<TreeViewNode> roots, Action<TreeViewNode, T> action) where T : class
    {
        TraverseDepthFirst(roots, node =>
        {
            if (node.Content is T typedContent)
            {
                action(node, typedContent);
            }
        });
    }

    /// <summary>
    /// Finds the lowest common ancestor of two nodes.
    /// </summary>
    public static TreeViewNode? FindLowestCommonAncestor(TreeViewNode node1, TreeViewNode node2)
    {
        if (node1 == null || node2 == null) return null;

        var ancestors1 = new HashSet<TreeViewNode>(GetAncestors(node1));
        ancestors1.Add(node1);

        var current = node2;
        while (current != null)
        {
            if (ancestors1.Contains(current))
                return current;

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// Gets the path from root to the given node as a list of nodes.
    /// </summary>
    public static List<TreeViewNode> GetPath(TreeViewNode node)
    {
        var path = new List<TreeViewNode>();
        var current = node;

        while (current != null)
        {
            path.Insert(0, current); // Insert at beginning to maintain root-to-node order
            current = current.Parent;
        }

        return path;
    }

    /// <summary>
    /// Validates that a tree has no circular references (infinite safety check).
    /// Returns true if tree is valid, false if circular reference detected.
    /// </summary>
    public static bool ValidateTreeStructure(TreeViewNode root, int maxDepth = 100)
    {
        try
        {
            var visited = new HashSet<TreeViewNode>();
            return ValidateNode(root, visited, 0, maxDepth);
        }
        catch
        {
            return false;
        }
    }

    private static bool ValidateNode(TreeViewNode node, HashSet<TreeViewNode> visited, int currentDepth, int maxDepth)
    {
        if (node == null) return true;
        if (currentDepth > maxDepth) return false; // Depth limit exceeded
        if (visited.Contains(node)) return false; // Circular reference detected

        visited.Add(node);

        foreach (var child in node.Children)
        {
            if (!ValidateNode(child, visited, currentDepth + 1, maxDepth))
                return false;
        }

        return true;
    }
}
