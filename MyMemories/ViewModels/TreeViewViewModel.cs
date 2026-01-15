using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Commands;
using MyMemories.Models;
using MyMemories.Services;

namespace MyMemories.ViewModels;

/// <summary>
/// ViewModel for managing tree view operations and state.
/// </summary>
public class TreeViewViewModel : ViewModelBase
{
    private readonly TreeViewService _treeViewService;
    private readonly CategoryService _categoryService;
    private readonly LinkSelectionService _linkSelectionService;
    
    private TreeViewNode? _selectedNode;
    private TreeViewNode? _contextMenuNode;

    /// <summary>
    /// Initializes a new instance of TreeViewViewModel.
    /// </summary>
    public TreeViewViewModel(
        TreeViewService treeViewService,
        CategoryService categoryService,
        LinkSelectionService linkSelectionService)
    {
        _treeViewService = treeViewService ?? throw new ArgumentNullException(nameof(treeViewService));
        _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
        _linkSelectionService = linkSelectionService ?? throw new ArgumentNullException(nameof(linkSelectionService));

        InitializeCommands();
    }

    #region Properties

    /// <summary>
    /// Gets or sets the currently selected tree node.
    /// </summary>
    public TreeViewNode? SelectedNode
    {
        get => _selectedNode;
        set => SetProperty(ref _selectedNode, value, OnSelectedNodeChanged);
    }

    /// <summary>
    /// Gets or sets the node that triggered the context menu.
    /// </summary>
    public TreeViewNode? ContextMenuNode
    {
        get => _contextMenuNode;
        set => SetProperty(ref _contextMenuNode, value);
    }

    /// <summary>
    /// Gets whether a node is currently selected.
    /// </summary>
    public bool HasSelectedNode => SelectedNode != null;

    /// <summary>
    /// Gets whether the selected node is a category.
    /// </summary>
    public bool IsSelectedNodeCategory => SelectedNode?.Content is CategoryItem;

    /// <summary>
    /// Gets whether the selected node is a link.
    /// </summary>
    public bool IsSelectedNodeLink => SelectedNode?.Content is LinkItem;

    #endregion

    #region Commands

    /// <summary>
    /// Command to handle node selection.
    /// </summary>
    public ICommand SelectNodeCommand { get; private set; } = null!;

    /// <summary>
    /// Command to expand a node.
    /// </summary>
    public ICommand ExpandNodeCommand { get; private set; } = null!;

    /// <summary>
    /// Command to collapse a node.
    /// </summary>
    public ICommand CollapseNodeCommand { get; private set; } = null!;

    /// <summary>
    /// Command to refresh a node.
    /// </summary>
    public ICommand RefreshNodeCommand { get; private set; } = null!;

    #endregion

    #region Initialization

    private void InitializeCommands()
    {
        SelectNodeCommand = new RelayCommand<TreeViewNode>(SelectNode, node => node != null);
        ExpandNodeCommand = new RelayCommand<TreeViewNode>(ExpandNode, node => node != null);
        CollapseNodeCommand = new RelayCommand<TreeViewNode>(CollapseNode, node => node != null);
        RefreshNodeCommand = new AsyncRelayCommand<TreeViewNode>(
            async node => await RefreshNodeAsync(node),
            node => node != null);
    }

    #endregion

    #region Node Operations

    /// <summary>
    /// Selects a tree node.
    /// </summary>
    private void SelectNode(TreeViewNode? node)
    {
        SelectedNode = node;
    }

    /// <summary>
    /// Expands a tree node.
    /// </summary>
    private void ExpandNode(TreeViewNode? node)
    {
        if (node != null)
        {
            node.IsExpanded = true;
        }
    }

    /// <summary>
    /// Collapses a tree node.
    /// </summary>
    private void CollapseNode(TreeViewNode? node)
    {
        if (node != null)
        {
            node.IsExpanded = false;
        }
    }

    /// <summary>
    /// Refreshes a tree node.
    /// </summary>
    private async Task RefreshNodeAsync(TreeViewNode? node)
    {
        if (node == null)
            return;

        // Implement refresh logic
        await Task.CompletedTask;
    }

    /// <summary>
    /// Called when the selected node changes.
    /// </summary>
    private void OnSelectedNodeChanged()
    {
        OnPropertyChanged(nameof(HasSelectedNode));
        OnPropertyChanged(nameof(IsSelectedNodeCategory));
        OnPropertyChanged(nameof(IsSelectedNodeLink));

        // TODO: Add HandleNodeSelection() method to LinkSelectionService
        // or call the appropriate existing method (e.g., HandleLinkSelectionAsync)
        // if (SelectedNode != null)
        // {
        //     _linkSelectionService.HandleNodeSelection(SelectedNode);
        // }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the root category node for a given node.
    /// TODO: Add GetRootCategoryNode() to TreeViewService or implement logic here
    /// </summary>
    public TreeViewNode? GetRootCategoryNode(TreeViewNode node)
    {
        // Temporary implementation - traverse up to root
        var current = node;
        while (current?.Parent != null)
        {
            current = current.Parent;
        }
        return current?.Content is CategoryItem ? current : null;
    }

    /// <summary>
    /// Inserts a category node into the tree.
    /// </summary>
    public void InsertCategoryNode(TreeViewNode node)
    {
        _treeViewService.InsertCategoryNode(node);
    }

    /// <summary>
    /// Gets the root node count.
    /// TODO: Add GetRootNodeCount() to TreeViewService or access TreeView directly
    /// </summary>
    public int GetRootNodeCount()
    {
        // This method needs TreeView access - should be added to TreeViewService
        throw new NotImplementedException("GetRootNodeCount needs to be added to TreeViewService");
    }

    #endregion
}
