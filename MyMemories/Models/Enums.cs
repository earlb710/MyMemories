namespace MyMemories;

/// <summary>
/// URL accessibility status for bookmark links.
/// </summary>
public enum UrlStatus
{
    Unknown = 0,     // Not checked yet
    Accessible = 1,  // Green - URL is accessible
    Error = 2,       // Yellow - URL returned an error
    NotFound = 3     // Red - URL does not exist (404, DNS failure, etc.)
}

/// <summary>
/// Browser types for bookmark import.
/// </summary>
public enum BrowserType
{
    Chrome,
    Edge,
    Brave,
    Vivaldi,
    Opera,
    Firefox
}

/// <summary>
/// Password protection type for categories.
/// </summary>
public enum PasswordProtectionType
{
    None = 0,
    GlobalPassword = 1,
    OwnPassword = 2
}

/// <summary>
/// Sort options for categories and catalog items.
/// </summary>
public enum SortOption
{
    NameAscending,
    NameDescending,
    SizeAscending,
    SizeDescending,
    DateAscending,
    DateDescending
}

/// <summary>
/// Type of folder link.
/// </summary>
public enum FolderLinkType
{
    LinkOnly,
    CatalogueFiles,
    FilteredCatalogue
}

/// <summary>
/// Type of link (URL, File, Folder, Git repository).
/// </summary>
public enum LinkType
{
    Unknown,  // Not set or needs to be guessed
    URL,      // Web URL (http:// or https://)
    File,     // File path
    Folder,   // Directory/folder path
    Git       // Git repository
}
