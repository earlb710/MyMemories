using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyMemories.Services;

/// <summary>
/// Service for managing Git repository configuration in git.json file.
/// </summary>
public class GitConfigService
{
    private readonly string _gitConfigFilePath;
    private GitConfiguration _gitConfig;

    public GitConfigService()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyMemories"
        );
        Directory.CreateDirectory(appDataFolder);
        
        _gitConfigFilePath = Path.Combine(appDataFolder, "git.json");
        _gitConfig = new GitConfiguration();
    }

    /// <summary>
    /// Load Git configuration from git.json file.
    /// </summary>
    public async Task LoadAsync()
    {
        if (File.Exists(_gitConfigFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_gitConfigFilePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };
                
                // Try to deserialize as new array format first
                try
                {
                    _gitConfig = JsonSerializer.Deserialize<GitConfiguration>(json, options) ?? new GitConfiguration();
                }
                catch (JsonException)
                {
                    // If that fails, try to migrate from old dictionary format
                    var oldConfig = JsonSerializer.Deserialize<OldGitConfiguration>(json, options);
                    if (oldConfig?.Repositories != null)
                    {
                        _gitConfig = new GitConfiguration();
                        foreach (var kvp in oldConfig.Repositories)
                        {
                            var config = kvp.Value;
                            config.Name = kvp.Key;
                            _gitConfig.Repositories.Add(config);
                        }
                        // Save the migrated configuration in the new format
                        await SaveAsync();
                    }
                    else
                    {
                        _gitConfig = new GitConfiguration();
                    }
                }
            }
            catch (Exception)
            {
                // If load fails, start with empty configuration
                _gitConfig = new GitConfiguration();
            }
        }
        else
        {
            _gitConfig = new GitConfiguration();
        }
    }

    /// <summary>
    /// Save Git configuration to git.json file.
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(_gitConfig, options);
            await File.WriteAllTextAsync(_gitConfigFilePath, json);
        }
        catch (Exception)
        {
            // Handle save errors silently or log them
            throw;
        }
    }

    /// <summary>
    /// Get all configured repositories.
    /// </summary>
    public List<GitRepositoryConfig> Repositories => _gitConfig.Repositories;

    /// <summary>
    /// Add or update a repository.
    /// </summary>
    public void AddOrUpdateRepository(string name, GitRepositoryConfig config)
    {
        config.Name = name;
        var existing = _gitConfig.Repositories.FirstOrDefault(r => r.Name == name);
        if (existing != null)
        {
            _gitConfig.Repositories.Remove(existing);
        }
        _gitConfig.Repositories.Add(config);
    }

    /// <summary>
    /// Remove a repository by name.
    /// </summary>
    public bool RemoveRepository(string name)
    {
        var repo = _gitConfig.Repositories.FirstOrDefault(r => r.Name == name);
        if (repo != null)
        {
            _gitConfig.Repositories.Remove(repo);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get a repository by name.
    /// </summary>
    public GitRepositoryConfig? GetRepository(string name)
    {
        return _gitConfig.Repositories.FirstOrDefault(r => r.Name == name);
    }
}

/// <summary>
/// Git configuration container.
/// </summary>
public class GitConfiguration
{
    public List<GitRepositoryConfig> Repositories { get; set; } = new();
}

/// <summary>
/// Old Git configuration format (dictionary-based) for migration.
/// </summary>
internal class OldGitConfiguration
{
    public Dictionary<string, GitRepositoryConfig> Repositories { get; set; } = new();
}

/// <summary>
/// Git repository configuration with all setup information.
/// </summary>
public class GitRepositoryConfig
{
    /// <summary>
    /// Repository name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Repository path or URL.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Username for authentication (optional).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Password for authentication (optional).
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Whether the repository connection was tested successfully.
    /// </summary>
    public bool Connected { get; set; } = false;

    /// <summary>
    /// Default branch name.
    /// </summary>
    public string DefaultBranch { get; set; } = "main";
    
    /// <summary>
    /// List of available branches fetched from the repository.
    /// </summary>
    public List<string> AvailableBranches { get; set; } = new();
    
    /// <summary>
    /// Currently selected branch.
    /// </summary>
    public string SelectedBranch { get; set; } = "main";
    
    /// <summary>
    /// Whether the repository has been cloned locally.
    /// </summary>
    public bool IsCloned { get; set; } = false;
    
    /// <summary>
    /// Local clone path (if cloned).
    /// </summary>
    public string LocalClonePath { get; set; } = string.Empty;
}
