using System;
using System.Collections.Generic;

namespace MyMemories;

/// <summary>
/// Represents a predefined rating type that can be applied to categories and links.
/// Rating definitions must be created before ratings can be assigned.
/// </summary>
public class RatingDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Color for this rating type (hex format). If empty, uses score-based color.
    /// </summary>
    public string Color { get; set; } = string.Empty;
    
    /// <summary>
    /// Minimum allowed score for this rating type. Default is -10.
    /// </summary>
    public int MinScore { get; set; } = -10;
    
    /// <summary>
    /// Maximum allowed score for this rating type. Default is 10.
    /// </summary>
    public int MaxScore { get; set; } = 10;
    
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Gets a display string for the score range.
    /// </summary>
    public string ScoreRangeDisplay => $"{MinScore} to {MaxScore}";
    
    public override string ToString() => Name;
}

/// <summary>
/// Represents a rating value applied to a category or link.
/// The score ranges from the definition's MinScore to MaxScore.
/// </summary>
public class RatingValue
{
    /// <summary>
    /// The qualified rating name in format "TemplateName.RatingName" (e.g., "Image.Focus").
    /// For the Default template, format is just "RatingName" (e.g., "Focus").
    /// </summary>
    public string Rating { get; set; } = string.Empty;
    
    /// <summary>
    /// Legacy property for backwards compatibility. Maps to Rating property.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string RatingDefinitionId
    {
        get => Rating;
        set => Rating = value;
    }
    
    /// <summary>
    /// The score for this rating.
    /// </summary>
    public int Score { get; set; }
    
    /// <summary>
    /// The reason or justification for this rating score.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// When this rating was first applied.
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// When this rating was last modified.
    /// </summary>
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Gets the template name from the qualified rating name.
    /// Returns empty string for Default template.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string TemplateName
    {
        get
        {
            var dotIndex = Rating.LastIndexOf('.');
            return dotIndex > 0 ? Rating.Substring(0, dotIndex) : string.Empty;
        }
    }
    
    /// <summary>
    /// Gets the rating name without template prefix.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string RatingName
    {
        get
        {
            var dotIndex = Rating.LastIndexOf('.');
            return dotIndex > 0 ? Rating.Substring(dotIndex + 1) : Rating;
        }
    }
}

/// <summary>
/// Represents an archived rating snapshot with timestamp.
/// Used to track rating history when ratings change.
/// </summary>
public class ArchivedRating
{
    /// <summary>
    /// The parent item name (category or link title).
    /// </summary>
    public string ParentName { get; set; } = string.Empty;
    
    /// <summary>
    /// The rating name being archived.
    /// </summary>
    public string RatingName { get; set; } = string.Empty;
    
    /// <summary>
    /// The previous rating value before the change.
    /// </summary>
    public RatingValue PreviousRating { get; set; } = new();
    
    /// <summary>
    /// When this rating was archived.
    /// </summary>
    public DateTime ArchivedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Reason for the rating change (optional).
    /// </summary>
    public string? ChangeReason { get; set; }
}

/// <summary>
/// Represents a named template containing a collection of rating definitions.
/// </summary>
public class RatingTemplate
{
    /// <summary>
    /// Template name. Empty string represents the "Default" template.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Rating definitions in this template.
    /// </summary>
    public List<RatingDefinition> Definitions { get; set; } = new();
    
    /// <summary>
    /// When this template was created.
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// When this template was last modified.
    /// </summary>
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
}

/// <summary>
/// Container for storing all rating templates and definitions.
/// </summary>
public class RatingDefinitionCollection
{
    /// <summary>
    /// The currently active template name. Empty string = Default.
    /// </summary>
    public string CurrentTemplateName { get; set; } = string.Empty;
    
    /// <summary>
    /// All rating templates. Always includes at least the Default template.
    /// </summary>
    public List<RatingTemplate> Templates { get; set; } = new();
    
    /// <summary>
    /// Legacy: Direct definitions list for backwards compatibility.
    /// New code should use Templates instead.
    /// </summary>
    public List<RatingDefinition> Definitions { get; set; } = new();
    
    public DateTime LastModified { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Gets the current template, creating Default if needed.
    /// </summary>
    public RatingTemplate GetCurrentTemplate()
    {
        // Migrate legacy definitions to Default template if needed
        if (Templates.Count == 0 && Definitions.Count > 0)
        {
            Templates.Add(new RatingTemplate
            {
                Name = string.Empty,
                Definitions = new List<RatingDefinition>(Definitions),
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now
            });
            Definitions.Clear();
        }
        
        // Ensure at least Default template exists
        if (Templates.Count == 0)
        {
            Templates.Add(new RatingTemplate
            {
                Name = string.Empty,
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now
            });
        }
        
        // Find current template
        var template = Templates.Find(t => t.Name == CurrentTemplateName);
        if (template == null)
        {
            // Fall back to Default
            CurrentTemplateName = string.Empty;
            template = Templates.Find(t => t.Name == string.Empty);
            
            // Create Default if somehow missing
            if (template == null)
            {
                template = new RatingTemplate
                {
                    Name = string.Empty,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                };
                Templates.Add(template);
            }
        }
        
        return template;
    }
}
