using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Dialogs;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Facade for displaying and editing link and category details.
/// This class now delegates to specialized dialog builders.
/// </summary>
public class LinkDetailsDialog
{
    private readonly LinkDetailsViewer _detailsViewer;
    private readonly LinkDialogBuilder _linkDialogBuilder;
    private readonly CategoryDialogBuilder _categoryDialogBuilder;
    private readonly ZipDialogBuilder _zipDialogBuilder;

    public LinkDetailsDialog(Window parentWindow, XamlRoot xamlRoot, ConfigurationService? configService = null)
    {
        _detailsViewer = new LinkDetailsViewer(xamlRoot);
        _linkDialogBuilder = new LinkDialogBuilder(parentWindow, xamlRoot);
        _categoryDialogBuilder = new CategoryDialogBuilder(xamlRoot, configService);
        _zipDialogBuilder = new ZipDialogBuilder(parentWindow, xamlRoot);
    }
    
    /// <summary>
    /// Sets the bookmark lookup categories for the link dialog builder.
    /// </summary>
    public void SetBookmarkLookupCategories(List<TreeViewNode> categories)
    {
        _linkDialogBuilder.SetBookmarkLookupCategories(categories);
    }

    public Task<bool> ShowAsync(LinkItem link) => 
        _detailsViewer.ShowAsync(link);

    public Task<AddLinkResult?> ShowAddAsync(IEnumerable<CategoryNode> categories, CategoryNode? selectedCategory) => 
        _linkDialogBuilder.ShowAddAsync(categories, selectedCategory);

    public Task<LinkEditResult?> ShowEditAsync(LinkItem link) => 
        _linkDialogBuilder.ShowEditAsync(link);

    public Task<MoveLinkResult?> ShowMoveLinkAsync(IEnumerable<CategoryNode> allCategories, TreeViewNode currentCategoryNode, string linkTitle) => 
        _categoryDialogBuilder.ShowMoveLinkAsync(allCategories, currentCategoryNode, linkTitle);

    public Task<CategoryEditResult?> ShowCategoryDialogAsync(string title, CategoryDialogOptions? options = null) => 
        _categoryDialogBuilder.ShowCategoryDialogAsync(title, options);

    public Task<ZipFolderResult?> ShowZipFolderDialogAsync(string folderTitle, string defaultTargetDirectory, string sourceFolderPath) => 
        _zipDialogBuilder.ShowZipFolderDialogAsync(folderTitle, defaultTargetDirectory, sourceFolderPath);

    public Task<ZipFolderResult?> ShowZipFolderDialogAsync(
        string folderTitle, 
        string defaultTargetDirectory, 
        string sourceFolderPath,
        Func<string, (bool isValid, string? errorMessage)>? validateZipName,
        TreeViewNode? parentCategoryNode) => 
        _zipDialogBuilder.ShowZipFolderDialogAsync(folderTitle, defaultTargetDirectory, sourceFolderPath, validateZipName, parentCategoryNode);

    public Task<ZipFolderResult?> ShowZipFolderDialogAsync(string folderTitle, string defaultTargetDirectory, string[] sourceFolderPaths) => 
        _zipDialogBuilder.ShowZipFolderDialogAsync(folderTitle, defaultTargetDirectory, sourceFolderPaths, false, null);

    public Task<ZipFolderResult?> ShowZipFolderDialogAsync(
        string folderTitle, 
        string defaultTargetDirectory, 
        string[] sourceFolderPaths,
        bool categoryHasPassword,
        string? categoryPassword) => 
        _zipDialogBuilder.ShowZipFolderDialogAsync(folderTitle, defaultTargetDirectory, sourceFolderPaths, categoryHasPassword, categoryPassword);

    public Task<ZipFolderResult?> ShowZipFolderDialogAsync(
        string folderTitle, 
        string defaultTargetDirectory, 
        string[] sourceFolderPaths,
        bool categoryHasPassword,
        string? categoryPassword,
        Func<string, (bool isValid, string? errorMessage)>? validateZipName,
        TreeViewNode? parentCategoryNode) => 
        _zipDialogBuilder.ShowZipFolderDialogAsync(folderTitle, defaultTargetDirectory, sourceFolderPaths, categoryHasPassword, categoryPassword, validateZipName, parentCategoryNode);
}
