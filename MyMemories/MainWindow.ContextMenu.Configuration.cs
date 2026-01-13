using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MyMemories.Services;
using System.IO;
using System.Linq;

namespace MyMemories;

/// <summary>
/// Context menu configuration and population logic.
/// Handles right-click events and dynamic menu setup.
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>
    /// Handles right-click events on the tree view.
    /// </summary>
    private void LinksTreeView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement element)
            return;

        var treeViewItem = FindParent<TreeViewItem>(element);
        if (treeViewItem?.Content is not TreeViewNode node)
            return;

        _contextMenuNode = node;

        MenuFlyout? menu = null;
        
        if (node.Content is CategoryItem category)
        {
            menu = LinksTreeView.Resources["CategoryContextMenu"] as MenuFlyout;
            if (menu != null)
            {
                ConfigureCategoryContextMenu(menu, category, node);
            }
        }
        else if (node.Content is LinkItem linkItem)
        {
            menu = LinksTreeView.Resources["LinkContextMenu"] as MenuFlyout;
            if (menu != null)
            {
                ConfigureLinkContextMenu(menu, linkItem, node);
            }
        }

        menu?.ShowAt(treeViewItem, e.GetPosition(treeViewItem));
        e.Handled = true;
    }

    /// <summary>
    /// Configures the category context menu based on the node's properties.
    /// OPTIMIZED: Uses cached lookups and fast in-memory checks.
    /// </summary>
    private void ConfigureCategoryContextMenu(MenuFlyout menu, CategoryItem category, TreeViewNode node)
    {
        // Get menu items by name (using cache for performance)
        var changePasswordItem = FindMenuItemByName(menu, "CategoryMenu_ChangePassword", _categoryMenuItemCache);
        var backupDirectoriesItem = FindMenuItemByName(menu, "CategoryMenu_BackupDirectories", _categoryMenuItemCache);
        var zipCategoryItem = FindMenuItemByName(menu, "CategoryMenu_ZipCategory", _categoryMenuItemCache);
        var addTagSubItem = FindSubMenuItemByName(menu, "CategoryMenu_AddTag", _categorySubMenuCache);
        var removeTagItem = FindMenuItemByName(menu, "CategoryMenu_RemoveTag", _categoryMenuItemCache);
        var ratingsSubItem = FindSubMenuItemByName(menu, "CategoryMenu_Ratings", _categorySubMenuCache);

        // Check if this node is a root category
        bool isRootCategory = LinksTreeView.RootNodes.Contains(node);
        
        // Change Password: only enabled for root categories
        if (changePasswordItem != null)
            changePasswordItem.IsEnabled = isRootCategory;

        // Backup Directories: only enabled for root categories
        if (backupDirectoriesItem != null)
            backupDirectoriesItem.IsEnabled = isRootCategory;

        // Zip Category: OPTIMIZED - fast check without I/O
        if (zipCategoryItem != null)
            zipCategoryItem.IsEnabled = HasDirectoryLinksRecursive(node);

        // Configure Add Tag submenu
        if (addTagSubItem != null)
            PopulateAddTagSubmenu(addTagSubItem, category.TagIds, isCategory: true);

        // Remove Tag: only enabled if category has tags
        if (removeTagItem != null)
            removeTagItem.IsEnabled = category.TagIds.Count > 0;

        // Configure Ratings submenu with templates (uses cache)
        if (ratingsSubItem != null)
            PopulateRatingsSubmenu(ratingsSubItem, category.Ratings.Count, isCategory: true);
    }

    /// <summary>
    /// Configures the link context menu based on the link's properties.
    /// OPTIMIZED: Uses cached lookups.
    /// </summary>
    private void ConfigureLinkContextMenu(MenuFlyout menu, LinkItem link, TreeViewNode node)
    {
        // Get menu items by name (using cache for performance)
        var addSubLinkItem = FindMenuItemByName(menu, "LinkMenu_AddSubLink", _linkMenuItemCache);
        var editItem = FindMenuItemByName(menu, "LinkMenu_Edit", _linkMenuItemCache);
        var copyItem = FindMenuItemByName(menu, "LinkMenu_Copy", _linkMenuItemCache);
        var moveItem = FindMenuItemByName(menu, "LinkMenu_Move", _linkMenuItemCache);
        var removeItem = FindMenuItemByName(menu, "LinkMenu_Remove", _linkMenuItemCache);
        var changePasswordItem = FindMenuItemByName(menu, "LinkMenu_ChangePassword", _linkMenuItemCache);
        var backupZipItem = FindMenuItemByName(menu, "LinkMenu_BackupZip", _linkMenuItemCache);
        var zipFolderItem = FindMenuItemByName(menu, "LinkMenu_ZipFolder", _linkMenuItemCache);
        var exploreHereItem = FindMenuItemByName(menu, "LinkMenu_ExploreHere", _linkMenuItemCache);
        var sortCatalogItem = FindMenuItemByName(menu, "LinkMenu_SortCatalog", _linkMenuItemCache);
        var summarizeItem = FindMenuItemByName(menu, "LinkMenu_Summarize", _linkMenuItemCache);
        var addTagSubItem = FindSubMenuItemByName(menu, "LinkMenu_AddTag", _linkSubMenuCache);
        var removeTagItem = FindMenuItemByName(menu, "LinkMenu_RemoveTag", _linkMenuItemCache);
        var ratingsSubItem = FindSubMenuItemByName(menu, "LinkMenu_Ratings", _linkSubMenuCache);

        // Check conditions
        bool isCatalogEntry = link.IsCatalogEntry;
        bool isZipFile = link.IsDirectory && link.Url.EndsWith(".zip", System.StringComparison.OrdinalIgnoreCase);
        bool isDirectory = link.IsDirectory;
        bool isZipOrDirectory = isZipFile || (isDirectory && Directory.Exists(link.Url));
        bool hasCatalog = isDirectory && node.Children.Count > 0;
        bool isUrl = System.Uri.TryCreate(link.Url, System.UriKind.Absolute, out var uri) && 
                     (uri.Scheme == System.Uri.UriSchemeHttp || uri.Scheme == System.Uri.UriSchemeHttps);

        // Configure menu items
        if (addSubLinkItem != null)
            addSubLinkItem.IsEnabled = !isCatalogEntry && !isDirectory;

        if (editItem != null)
            editItem.IsEnabled = !isCatalogEntry;
        if (copyItem != null)
            copyItem.IsEnabled = !isCatalogEntry;
        if (moveItem != null)
            moveItem.IsEnabled = !isCatalogEntry;
        if (removeItem != null)
            removeItem.IsEnabled = !isCatalogEntry;

        if (changePasswordItem != null)
            changePasswordItem.IsEnabled = isZipFile;

        if (backupZipItem != null)
            backupZipItem.IsEnabled = isZipFile && !isCatalogEntry;

        if (summarizeItem != null)
            summarizeItem.IsEnabled = isUrl;

        if (zipFolderItem != null)
            zipFolderItem.IsEnabled = isDirectory && !isZipFile && !isCatalogEntry;

        if (exploreHereItem != null)
            exploreHereItem.IsEnabled = isZipOrDirectory;

        if (sortCatalogItem != null)
            sortCatalogItem.IsEnabled = isDirectory && hasCatalog;

        if (addTagSubItem != null)
            PopulateAddTagSubmenu(addTagSubItem, link.TagIds, isCategory: false);

        if (removeTagItem != null)
            removeTagItem.IsEnabled = link.TagIds.Count > 0;

        if (ratingsSubItem != null)
            PopulateRatingsSubmenu(ratingsSubItem, link.Ratings.Count, isCategory: false);
    }

    /// <summary>
    /// Populates the Add Tag submenu with available tags.
    /// OPTIMIZED: Uses O(n) HashSet filtering instead of O(n×m) nested loops.
    /// </summary>
    private void PopulateAddTagSubmenu(MenuFlyoutSubItem subMenu, System.Collections.Generic.List<string> existingTags, bool isCategory)
    {
        subMenu.Items.Clear();

        if (_tagService == null || _tagService.TagCount == 0)
        {
            AddDisabledMenuItem(subMenu, "No tags available");
            return;
        }

        // OPTIMIZED: Use fast HashSet-based filtering
        var availableTags = GetAvailableTags(existingTags);

        if (availableTags.Count == 0)
        {
            AddDisabledMenuItem(subMenu, "All tags already assigned");
            return;
        }

        foreach (var tag in availableTags)
        {
            var tagItem = new MenuFlyoutItem
            {
                Text = $"??? {tag.Name}",
                Tag = tag.Name
            };

            tagItem.Click += isCategory 
                ? CategoryMenu_AddTagItem_Click 
                : LinkMenu_AddTagItem_Click;

            subMenu.Items.Add(tagItem);
        }
    }

    /// <summary>
    /// Populates the Ratings submenu with rating templates that have definitions.
    /// OPTIMIZED: Uses cached template list to avoid expensive template switching.
    /// </summary>
    private void PopulateRatingsSubmenu(MenuFlyoutSubItem subMenu, int currentRatingCount, bool isCategory)
    {
        subMenu.Items.Clear();

        if (_ratingService == null)
        {
            AddDisabledMenuItem(subMenu, "Rating service not available");
            return;
        }

        // Update the submenu text to show rating count
        subMenu.Text = currentRatingCount > 0 ? $"Ratings ({currentRatingCount})" : "Ratings";

        // OPTIMIZED: Use cached templates
        var templatesWithRatings = GetRatingTemplates();

        if (templatesWithRatings.Count == 0)
        {
            AddDisabledMenuItem(subMenu, "No rating templates with definitions");
            subMenu.Items.Add(new MenuFlyoutSeparator());
            AddManageRatingsOption(subMenu);
            return;
        }

        // Add template options
        foreach (var (name, displayName, count) in templatesWithRatings)
        {
            var templateItem = new MenuFlyoutItem
            {
                Text = $"? {displayName} ({count} types)",
                Tag = name
            };

            templateItem.Click += isCategory 
                ? CategoryMenu_RatingTemplateItem_Click 
                : LinkMenu_RatingTemplateItem_Click;

            subMenu.Items.Add(templateItem);
        }

        // Add separator and management option
        subMenu.Items.Add(new MenuFlyoutSeparator());
        AddManageRatingsOption(subMenu);
    }

    /// <summary>
    /// Adds the "Manage Templates" option to a ratings submenu.
    /// </summary>
    private void AddManageRatingsOption(MenuFlyoutSubItem subMenu)
    {
        var manageItem = new MenuFlyoutItem
        {
            Text = "Manage Templates...",
            Icon = new FontIcon { Glyph = "\uE713" }
        };
        manageItem.Click += async (s, e) =>
        {
            // Invalidate cache when managing templates
            InvalidateRatingTemplateCache();
            
            var ratingDialog = new Dialogs.RatingManagementDialog(Content.XamlRoot, _ratingService!);
            await ratingDialog.RefreshAndShowDialogAsync();
        };
        subMenu.Items.Add(manageItem);
    }
}
