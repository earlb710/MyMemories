using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyMemories.Services.Interfaces;

/// <summary>
/// Interface for the CategoryService that manages category data persistence and operations.
/// </summary>
public interface ICategoryService
{
    /// <summary>
    /// Sets or updates the configuration service for password management.
    /// </summary>
    void SetConfigurationService(ConfigurationService configService);

    /// <summary>
    /// Caches the global password for encryption/decryption operations.
    /// </summary>
    void CacheGlobalPassword(string password);

    /// <summary>
    /// Caches a category's own password for encryption/decryption operations.
    /// </summary>
    void CacheCategoryPassword(string categoryName, string password);

    /// <summary>
    /// Clears all cached passwords.
    /// </summary>
    void ClearPasswordCache();

    /// <summary>
    /// Loads all categories from JSON files (both encrypted and unencrypted).
    /// </summary>
    Task<List<TreeViewNode>> LoadAllCategoriesAsync();

    /// <summary>
    /// Saves a category to a JSON file (encrypted if password is set).
    /// </summary>
    Task SaveCategoryAsync(TreeViewNode categoryNode);

    /// <summary>
    /// Deletes a category file (both encrypted and unencrypted versions).
    /// </summary>
    Task DeleteCategoryAsync(string categoryName);
}
