using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MyMemories.Services;

/// <summary>
/// Service for managing rating definitions and providing rating display helpers.
/// Supports multiple named templates (collections of rating definitions).
/// </summary>
public class RatingManagementService
{
    private const string RatingsFileName = "ratings.json";
    private const int MaxRatingDefinitions = 20;
    
    // FontIcon glyphs from Segoe MDL2 Assets
    private const string StarGlyph = "\uE735";      // FavoriteStar - for high scores
    private const string LikeGlyph = "\uE8E1";      // Like - for good scores
    private const string AddGlyph = "\uE710";       // Add - for positive scores
    private const string RemoveGlyph = "\uE738";    // Remove - for neutral
    private const string DislikeGlyph = "\uE8E0";   // Dislike - for bad scores
    private const string CancelGlyph = "\uE711";    // Cancel/X - for very bad scores
    
    private readonly string _ratingsFilePath;
    private RatingDefinitionCollection _ratingCollection;
    
    /// <summary>
    /// Static instance for global access (set during initialization).
    /// </summary>
    public static RatingManagementService? Instance { get; private set; }

    public RatingManagementService(string dataDirectory)
    {
        _ratingsFilePath = Path.Combine(dataDirectory, RatingsFileName);
        _ratingCollection = new RatingDefinitionCollection();
        Instance = this;
    }

    /// <summary>
    /// Gets the maximum number of rating definitions allowed per template.
    /// </summary>
    public static int MaxDefinitionCount => MaxRatingDefinitions;

    /// <summary>
    /// Gets the current template name. Empty string = Default.
    /// </summary>
    public string CurrentTemplateName => _ratingCollection.CurrentTemplateName;

    /// <summary>
    /// Gets all rating definitions in the current template.
    /// </summary>
    public IReadOnlyList<RatingDefinition> Definitions => 
        _ratingCollection.GetCurrentTemplate().Definitions.AsReadOnly();

    /// <summary>
    /// Gets the count of current rating definitions in the current template.
    /// </summary>
    public int DefinitionCount => _ratingCollection.GetCurrentTemplate().Definitions.Count;

    /// <summary>
    /// Checks if the maximum rating definition limit has been reached in the current template.
    /// </summary>
    public bool IsAtMaxCapacity => DefinitionCount >= MaxRatingDefinitions;

    /// <summary>
    /// Gets all template names.
    /// </summary>
    public List<string> GetTemplateNames()
    {
        // Ensure at least Default exists
        _ratingCollection.GetCurrentTemplate();
        return _ratingCollection.Templates.Select(t => t.Name).ToList();
    }

    /// <summary>
    /// Checks if a template with the given name exists.
    /// </summary>
    public bool TemplateExists(string name)
    {
        return _ratingCollection.Templates.Exists(t => 
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Creates a new template with the given name.
    /// </summary>
    public bool CreateTemplate(string name)
    {
        if (TemplateExists(name))
            return false;

        var template = new RatingTemplate
        {
            Name = name,
            Definitions = new List<RatingDefinition>(),
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now
        };

        _ratingCollection.Templates.Add(template);
        _ratingCollection.CurrentTemplateName = name;
        return true;
    }

    /// <summary>
    /// Deletes a template by name. Cannot delete the last template.
    /// </summary>
    public bool DeleteTemplate(string name)
    {
        if (_ratingCollection.Templates.Count <= 1)
            return false;

        var template = _ratingCollection.Templates.Find(t => t.Name == name);
        if (template == null)
            return false;

        _ratingCollection.Templates.Remove(template);

        // Switch to another template if we deleted the current one
        if (_ratingCollection.CurrentTemplateName == name)
        {
            _ratingCollection.CurrentTemplateName = _ratingCollection.Templates.First().Name;
        }

        return true;
    }

    /// <summary>
    /// Switches to a different template.
    /// </summary>
    public bool SwitchTemplate(string name)
    {
        if (!TemplateExists(name))
            return false;

        _ratingCollection.CurrentTemplateName = name;
        return true;
    }

    /// <summary>
    /// Loads rating definitions from the file system.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_ratingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_ratingsFilePath);
                var collection = JsonSerializer.Deserialize<RatingDefinitionCollection>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (collection != null)
                {
                    _ratingCollection = collection;
                    // Ensure template structure is valid
                    _ratingCollection.GetCurrentTemplate();
                }
            }
            else
            {
                // First run - create default templates
                _ratingCollection = new RatingDefinitionCollection();
                CreateDefaultTemplates();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading ratings: {ex.Message}");
            _ratingCollection = new RatingDefinitionCollection();
            CreateDefaultTemplates();
        }
    }

    /// <summary>
    /// Creates the default rating templates with predefined rating definitions.
    /// </summary>
    private void CreateDefaultTemplates()
    {
        // Default (General) template
        var defaultTemplate = new RatingTemplate
        {
            Name = string.Empty, // Empty = Default
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            Definitions = new List<RatingDefinition>
            {
                new() { Name = "Quality", Description = "Overall quality assessment", MinScore = -10, MaxScore = 10 },
                new() { Name = "Usefulness", Description = "How useful is this resource", MinScore = -10, MaxScore = 10 },
                new() { Name = "Relevance", Description = "How relevant to current needs", MinScore = -10, MaxScore = 10 },
                new() { Name = "Priority", Description = "Priority level for action", MinScore = 0, MaxScore = 10 }
            }
        };
        _ratingCollection.Templates.Add(defaultTemplate);

        // Image template
        var imageTemplate = new RatingTemplate
        {
            Name = "Image",
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            Definitions = new List<RatingDefinition>
            {
                new() { Name = "Composition", Description = "Visual composition and framing", MinScore = -10, MaxScore = 10 },
                new() { Name = "Focus", Description = "Sharpness and focus quality", MinScore = -10, MaxScore = 10 },
                new() { Name = "Lighting", Description = "Lighting and exposure quality", MinScore = -10, MaxScore = 10 },
                new() { Name = "Color", Description = "Color accuracy and appeal", MinScore = -10, MaxScore = 10 },
                new() { Name = "Subject", Description = "Subject matter interest", MinScore = -10, MaxScore = 10 },
                new() { Name = "Technical", Description = "Technical quality (resolution, noise)", MinScore = -10, MaxScore = 10 }
            }
        };
        _ratingCollection.Templates.Add(imageTemplate);

        // Programming template
        var programmingTemplate = new RatingTemplate
        {
            Name = "Programming",
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            Definitions = new List<RatingDefinition>
            {
                new() { Name = "CodeQuality", Description = "Code readability and maintainability", MinScore = -10, MaxScore = 10 },
                new() { Name = "Documentation", Description = "Quality of documentation and comments", MinScore = -10, MaxScore = 10 },
                new() { Name = "Performance", Description = "Efficiency and performance", MinScore = -10, MaxScore = 10 },
                new() { Name = "Complexity", Description = "Appropriate complexity level", MinScore = -10, MaxScore = 10 },
                new() { Name = "Reusability", Description = "How reusable is this code", MinScore = -10, MaxScore = 10 },
                new() { Name = "Testing", Description = "Test coverage and quality", MinScore = -10, MaxScore = 10 },
                new() { Name = "Security", Description = "Security considerations", MinScore = -10, MaxScore = 10 }
            }
        };
        _ratingCollection.Templates.Add(programmingTemplate);

        // Project template
        var projectTemplate = new RatingTemplate
        {
            Name = "Project",
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            Definitions = new List<RatingDefinition>
            {
                new() { Name = "Progress", Description = "Current progress toward completion", MinScore = 0, MaxScore = 10 },
                new() { Name = "Risk", Description = "Risk level (higher = more risky)", MinScore = 0, MaxScore = 10 },
                new() { Name = "Impact", Description = "Potential impact when completed", MinScore = -10, MaxScore = 10 },
                new() { Name = "Effort", Description = "Effort required (higher = more effort)", MinScore = 0, MaxScore = 10 },
                new() { Name = "Urgency", Description = "Time sensitivity", MinScore = 0, MaxScore = 10 },
                new() { Name = "Feasibility", Description = "How feasible is this project", MinScore = -10, MaxScore = 10 }
            }
        };
        _ratingCollection.Templates.Add(projectTemplate);

        // Video template
        var videoTemplate = new RatingTemplate
        {
            Name = "Video",
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            Definitions = new List<RatingDefinition>
            {
                new() { Name = "Content", Description = "Quality of content and information", MinScore = -10, MaxScore = 10 },
                new() { Name = "Production", Description = "Production quality (audio, video)", MinScore = -10, MaxScore = 10 },
                new() { Name = "Engagement", Description = "How engaging and entertaining", MinScore = -10, MaxScore = 10 },
                new() { Name = "Pacing", Description = "Pacing and flow", MinScore = -10, MaxScore = 10 },
                new() { Name = "Rewatchable", Description = "Worth watching again", MinScore = -10, MaxScore = 10 }
            }
        };
        _ratingCollection.Templates.Add(videoTemplate);

        // Philosophy template
        var philosophyTemplate = new RatingTemplate
        {
            Name = "Philosophy",
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            Definitions = new List<RatingDefinition>
            {
                new() { Name = "Clarity", Description = "Clarity of argument and expression", MinScore = -10, MaxScore = 10 },
                new() { Name = "Originality", Description = "Originality of thought", MinScore = -10, MaxScore = 10 },
                new() { Name = "Rigor", Description = "Logical rigor and coherence", MinScore = -10, MaxScore = 10 },
                new() { Name = "Depth", Description = "Depth of analysis", MinScore = -10, MaxScore = 10 },
                new() { Name = "Practical", Description = "Practical applicability", MinScore = -10, MaxScore = 10 },
                new() { Name = "Influence", Description = "Influence on thinking", MinScore = -10, MaxScore = 10 }
            }
        };
        _ratingCollection.Templates.Add(philosophyTemplate);

        // Book template
        var bookTemplate = new RatingTemplate
        {
            Name = "Book",
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            Definitions = new List<RatingDefinition>
            {
                new() { Name = "Writing", Description = "Quality of writing and prose", MinScore = -10, MaxScore = 10 },
                new() { Name = "Plot", Description = "Story structure and plot development", MinScore = -10, MaxScore = 10 },
                new() { Name = "Characters", Description = "Character development and depth", MinScore = -10, MaxScore = 10 },
                new() { Name = "Insight", Description = "Insights and ideas presented", MinScore = -10, MaxScore = 10 },
                new() { Name = "Engagement", Description = "How engaging and readable", MinScore = -10, MaxScore = 10 },
                new() { Name = "Memorable", Description = "Lasting impact and memorability", MinScore = -10, MaxScore = 10 },
                new() { Name = "Recommend", Description = "Would recommend to others", MinScore = -10, MaxScore = 10 }
            }
        };
        _ratingCollection.Templates.Add(bookTemplate);

        // Website template
        var websiteTemplate = new RatingTemplate
        {
            Name = "Website",
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            Definitions = new List<RatingDefinition>
            {
                new() { Name = "Content", Description = "Quality and accuracy of content", MinScore = -10, MaxScore = 10 },
                new() { Name = "Design", Description = "Visual design and aesthetics", MinScore = -10, MaxScore = 10 },
                new() { Name = "Usability", Description = "Ease of navigation and use", MinScore = -10, MaxScore = 10 },
                new() { Name = "Speed", Description = "Page load speed and performance", MinScore = -10, MaxScore = 10 },
                new() { Name = "Credibility", Description = "Trustworthiness and authority", MinScore = -10, MaxScore = 10 },
                new() { Name = "Updates", Description = "Frequency of updates and freshness", MinScore = -10, MaxScore = 10 },
                new() { Name = "Ads", Description = "Ad intrusiveness (higher = less intrusive)", MinScore = -10, MaxScore = 10 }
            }
        };
        _ratingCollection.Templates.Add(websiteTemplate);

        // Set Default as current
        _ratingCollection.CurrentTemplateName = string.Empty;
    }

    /// <summary>
    /// Saves rating definitions to the file system.
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            _ratingCollection.LastModified = DateTime.Now;
            
            // Update current template's modified date
            var currentTemplate = _ratingCollection.GetCurrentTemplate();
            currentTemplate.ModifiedDate = DateTime.Now;

            var json = JsonSerializer.Serialize(_ratingCollection, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var directory = Path.GetDirectoryName(_ratingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_ratingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving ratings: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Adds a new rating definition to the current template.
    /// </summary>
    /// <returns>True if the definition was added, false if at max capacity.</returns>
    public bool AddDefinition(RatingDefinition definition)
    {
        if (IsAtMaxCapacity)
            return false;

        definition.CreatedDate = DateTime.Now;
        definition.ModifiedDate = DateTime.Now;
        _ratingCollection.GetCurrentTemplate().Definitions.Add(definition);
        return true;
    }

    /// <summary>
    /// Updates an existing rating definition in the current template.
    /// </summary>
    public bool UpdateDefinition(RatingDefinition definition)
    {
        var definitions = _ratingCollection.GetCurrentTemplate().Definitions;
        var existingIndex = definitions.FindIndex(d => d.Id == definition.Id);
        if (existingIndex >= 0)
        {
            definition.ModifiedDate = DateTime.Now;
            definition.CreatedDate = definitions[existingIndex].CreatedDate;
            definitions[existingIndex] = definition;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes a rating definition by ID from the current template.
    /// </summary>
    public bool RemoveDefinition(string definitionId)
    {
        var definitions = _ratingCollection.GetCurrentTemplate().Definitions;
        var definition = definitions.Find(d => d.Id == definitionId);
        if (definition != null)
        {
            definitions.Remove(definition);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the qualified rating name for a definition in a template.
    /// Format: "TemplateName.RatingName" or just "RatingName" for Default template.
    /// </summary>
    public static string GetQualifiedRatingName(string templateName, string ratingName)
    {
        if (string.IsNullOrEmpty(templateName))
        {
            return ratingName;
        }
        return $"{templateName}.{ratingName}";
    }

    /// <summary>
    /// Parses a qualified rating name into template and rating components.
    /// </summary>
    public static (string TemplateName, string RatingName) ParseQualifiedRatingName(string qualifiedName)
    {
        if (string.IsNullOrEmpty(qualifiedName))
            return (string.Empty, string.Empty);
            
        var dotIndex = qualifiedName.LastIndexOf('.');
        if (dotIndex > 0)
        {
            return (qualifiedName.Substring(0, dotIndex), qualifiedName.Substring(dotIndex + 1));
        }
        return (string.Empty, qualifiedName);
    }

    /// <summary>
    /// Gets the qualified name for a rating definition in the current template.
    /// </summary>
    public string GetQualifiedName(RatingDefinition definition)
    {
        return GetQualifiedRatingName(_ratingCollection.CurrentTemplateName, definition.Name);
    }

    /// <summary>
    /// Gets a rating definition by qualified name (Template.RatingName format).
    /// Falls back to searching by GUID for legacy data.
    /// </summary>
    public RatingDefinition? GetDefinitionByQualifiedName(string qualifiedName)
    {
        if (string.IsNullOrEmpty(qualifiedName))
            return null;

        var (templateName, ratingName) = ParseQualifiedRatingName(qualifiedName);

        // Find the template
        var template = _ratingCollection.Templates.Find(t => 
            string.Equals(t.Name, templateName, StringComparison.OrdinalIgnoreCase));
        
        if (template != null)
        {
            // Find the definition by name within the template
            var definition = template.Definitions.Find(d => 
                string.Equals(d.Name, ratingName, StringComparison.OrdinalIgnoreCase));
            if (definition != null)
                return definition;
        }

        // Fallback: try to find by GUID (legacy support)
        if (Guid.TryParse(qualifiedName, out _))
        {
            foreach (var t in _ratingCollection.Templates)
            {
                var def = t.Definitions.Find(d => d.Id == qualifiedName);
                if (def != null)
                    return def;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets a rating definition by ID from all templates.
    /// Supports both qualified names (Template.RatingName) and legacy GUIDs.
    /// </summary>
    public RatingDefinition? GetDefinition(string definitionId)
    {
        // Try qualified name lookup first
        var definition = GetDefinitionByQualifiedName(definitionId);
        if (definition != null)
            return definition;

        // Legacy: search by GUID ID across all templates
        foreach (var template in _ratingCollection.Templates)
        {
            definition = template.Definitions.Find(d => d.Id == definitionId);
            if (definition != null)
                return definition;
        }

        return null;
    }

    /// <summary>
    /// Gets a rating definition by name (case-insensitive) from the current template.
    /// </summary>
    public RatingDefinition? GetDefinitionByName(string name)
    {
        return _ratingCollection.GetCurrentTemplate().Definitions.Find(d => 
            string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a rating definition name already exists in the current template.
    /// </summary>
    public bool DefinitionNameExists(string name, string? excludeId = null)
    {
        return _ratingCollection.GetCurrentTemplate().Definitions.Exists(d => 
            string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase) &&
            (excludeId == null || d.Id != excludeId));
    }

    /// <summary>
    /// Gets the color for a rating score.
    /// </summary>
    public static Color GetScoreColor(int score)
    {
        return score switch
        {
            >= 8 => Color.FromArgb(255, 0, 200, 0),      // Bright Green
            >= 5 => Color.FromArgb(255, 100, 180, 0),   // Green-Yellow
            >= 1 => Color.FromArgb(255, 150, 150, 0),   // Yellow
            0 => Colors.Gray,
            >= -4 => Color.FromArgb(255, 200, 100, 0),  // Orange
            >= -7 => Color.FromArgb(255, 220, 50, 0),   // Red-Orange
            _ => Color.FromArgb(255, 200, 0, 0)         // Red
        };
    }

    /// <summary>
    /// Gets a FontIcon glyph for a rating score.
    /// </summary>
    public static string GetScoreIconGlyph(int score)
    {
        return score switch
        {
            >= 8 => StarGlyph,      // Star - excellent
            >= 5 => LikeGlyph,      // Like/thumbs up - good
            >= 1 => AddGlyph,       // Plus - positive
            0 => RemoveGlyph,       // Minus - neutral
            >= -4 => DislikeGlyph,  // Dislike - bad
            _ => CancelGlyph        // X - very bad
        };
    }

    /// <summary>
    /// Gets a text label for a rating score.
    /// </summary>
    public static string GetScoreLabel(int score)
    {
        return score switch
        {
            >= 8 => "Excellent",
            >= 5 => "Good",
            >= 1 => "Positive",
            0 => "Neutral",
            >= -4 => "Poor",
            _ => "Very Poor"
        };
    }

    /// <summary>
    /// Formats a score for display with sign.
    /// </summary>
    public static string FormatScore(int score)
    {
        return score switch
        {
            > 0 => $"+{score}",
            0 => "0",
            _ => score.ToString()
        };
    }

    /// <summary>
    /// Gets formatted display text for a list of ratings.
    /// If a rating definition is not found, displays the raw rating name with score.
    /// </summary>
    public string GetRatingsDisplayText(IEnumerable<RatingValue> ratings)
    {
        if (ratings == null || !ratings.Any())
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var rating in ratings)
        {
            var definition = GetDefinition(rating.Rating);
            if (sb.Length > 0)
                sb.Append("  ");
            
            if (definition != null)
            {
                sb.Append($"{definition.Name}: {FormatScore(rating.Score)}");
            }
            else
            {
                // Show raw rating name for orphaned ratings
                sb.Append($"{rating.Rating}: {FormatScore(rating.Score)}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Calculates the average rating from a list of ratings.
    /// </summary>
    public static double CalculateAverageRating(IEnumerable<RatingValue> ratings)
    {
        if (ratings == null || !ratings.Any())
            return 0;

        return ratings.Average(r => r.Score);
    }

    /// <summary>
    /// Gets rating information for display (name, score, reason) for a list of ratings.
    /// Returns a list of tuples with (DefinitionName, Score, Reason) for each rating.
    /// If definition not found, uses the raw rating name.
    /// </summary>
    public List<(string Name, int Score, string Reason)> GetRatingsInfo(IEnumerable<RatingValue> ratings)
    {
        var result = new List<(string Name, int Score, string Reason)>();
        if (ratings == null)
            return result;

        foreach (var rating in ratings)
        {
            var definition = GetDefinition(rating.Rating);
            var displayName = definition?.Name ?? rating.Rating;
            result.Add((displayName, rating.Score, rating.Reason));
        }
        return result;
    }

    /// <summary>
    /// Creates a styled StackPanel with rating badges for display in UI.
    /// Includes orphaned ratings with their raw names.
    /// </summary>
    public StackPanel CreateRatingBadgesPanel(IEnumerable<RatingValue> ratings, double fontSize = 11, double spacing = 6)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = spacing
        };

        if (ratings == null)
            return panel;

        foreach (var rating in ratings)
        {
            var definition = GetDefinition(rating.Rating);
            var badge = CreateRatingBadgeWithFallback(definition, rating, fontSize);
            panel.Children.Add(badge);
        }

        return panel;
    }

    /// <summary>
    /// Creates a rating badge, using the raw rating name if definition is not found.
    /// </summary>
    private Border CreateRatingBadgeWithFallback(RatingDefinition? definition, RatingValue rating, double fontSize = 11)
    {
        var backgroundColor = GetScoreColor(rating.Score);
        var displayName = definition?.Name ?? rating.Rating;
        
        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };

        contentPanel.Children.Add(new FontIcon
        {
            Glyph = GetScoreIconGlyph(rating.Score),
            FontSize = fontSize,
            Foreground = new SolidColorBrush(Colors.White)
        });

        contentPanel.Children.Add(new TextBlock
        {
            Text = $"{displayName}: {FormatScore(rating.Score)}",
            FontSize = fontSize,
            Foreground = new SolidColorBrush(Colors.White),
            VerticalAlignment = VerticalAlignment.Center
        });

        var badge = new Border
        {
            Background = new SolidColorBrush(backgroundColor),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Child = contentPanel
        };

        // Add tooltip with reason if available
        if (!string.IsNullOrWhiteSpace(rating.Reason))
        {
            ToolTipService.SetToolTip(badge, rating.Reason);
        }

        return badge;
    }

    /// <summary>
    /// Creates a single rating badge with icon, name, score, and colored background.
    /// </summary>
    public Border CreateRatingBadge(RatingDefinition definition, RatingValue rating, double fontSize = 11)
    {
        return CreateRatingBadgeWithFallback(definition, rating, fontSize);
    }

    /// <summary>
    /// Creates a styled StackPanel with small rating score badges for tree node display.
    /// Limited to 3 visible ratings, with a "+N" indicator for additional ratings.
    /// Includes orphaned ratings.
    /// </summary>
    public StackPanel CreateRatingIconsPanel(IEnumerable<RatingValue> ratings, double iconSize = 10, double spacing = 2)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = spacing,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (ratings == null)
            return panel;

        var ratingsList = ratings.ToList();
        if (ratingsList.Count == 0)
            return panel;

        // Show up to 3 rating icons
        int displayCount = Math.Min(ratingsList.Count, 3);
        for (int i = 0; i < displayCount; i++)
        {
            var rating = ratingsList[i];
            var definition = GetDefinition(rating.Rating);
            var badge = CreateRatingIconBadgeWithFallback(definition, rating, iconSize);
            panel.Children.Add(badge);
        }

        // Show "+N" for additional ratings
        if (ratingsList.Count > 3)
        {
            var moreText = new TextBlock
            {
                Text = $"+{ratingsList.Count - 3}",
                FontSize = iconSize,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(2, 0, 0, 0)
            };
            panel.Children.Add(moreText);
        }

        return panel;
    }

    /// <summary>
    /// Creates a rating icon badge, using raw rating name if definition not found.
    /// </summary>
    private Border CreateRatingIconBadgeWithFallback(RatingDefinition? definition, RatingValue rating, double iconSize = 10)
    {
        var backgroundColor = GetScoreColor(rating.Score);
        var displayName = definition?.Name ?? rating.Rating;

        var scoreText = new TextBlock
        {
            Text = FormatScore(rating.Score),
            FontSize = iconSize,
            Foreground = new SolidColorBrush(Colors.White),
            VerticalAlignment = VerticalAlignment.Center
        };

        var badge = new Border
        {
            Background = new SolidColorBrush(backgroundColor),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Child = scoreText,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Add tooltip with definition name and reason
        var tooltip = displayName;
        if (!string.IsNullOrWhiteSpace(rating.Reason))
        {
            tooltip += $": {rating.Reason}";
        }
        ToolTipService.SetToolTip(badge, tooltip);

        return badge;
    }

    /// <summary>
    /// Creates a single small rating icon badge with colored background for tree node display.
    /// </summary>
    public Border CreateRatingIconBadge(RatingDefinition definition, RatingValue rating, double iconSize = 10)
    {
        return CreateRatingIconBadgeWithFallback(definition, rating, iconSize);
    }

    /// <summary>
    /// Creates a FontIcon for a score.
    /// </summary>
    public static FontIcon CreateScoreIcon(int score, double fontSize = 12)
    {
        return new FontIcon
        {
            Glyph = GetScoreIconGlyph(score),
            FontSize = fontSize,
            Foreground = new SolidColorBrush(GetScoreColor(score))
        };
    }
}
