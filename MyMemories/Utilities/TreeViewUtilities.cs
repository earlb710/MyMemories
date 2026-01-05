using System;
using Microsoft.UI.Xaml.Controls;

namespace MyMemories.Utilities;

public static class TreeViewUtilities
{
    /// <summary>
    /// Finds the root ancestor of a node (max depth protection included).
    /// </summary>
    public static TreeViewNode GetRootNode(
        TreeViewNode node, 
        int maxDepth = 100)
    {
        var current = node;
        int safetyCounter = 0;
        
        while (current.Parent != null && safetyCounter < maxDepth)
        {
            current = current.Parent;
            safetyCounter++;
        }
        
        if (safetyCounter >= maxDepth)
        {
            throw new InvalidOperationException(
                $"Node hierarchy too deep (>{maxDepth} levels).");
        }
        
        return current;
    }

    /// <summary>
    /// Recursively searches for a node matching a predicate.
    /// </summary>
    public static TreeViewNode? FindNode(
        TreeViewNode root, 
        Func<TreeViewNode, bool> predicate)
    {
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
}