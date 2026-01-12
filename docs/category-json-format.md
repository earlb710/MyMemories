# MyMemories Category JSON Format

This document describes the JSON format used to store category data in MyMemories. Each root category is saved as a separate JSON file in the data directory.

## File Location

- **Unencrypted categories**: `{DataFolder}/{CategoryName}.json`
- **Encrypted categories**: `{DataFolder}/{CategoryName}.zip.json` (AES-256 encrypted)

## Overview

The JSON structure is hierarchical, supporting:
- Categories with nested subcategories
- Links (URLs, files, and directories)
- Catalog entries (auto-generated file listings)
- Sub-links (child links under URL bookmarks)
- Tags for organization
- URL status tracking for web bookmarks

---

## CategoryData Object

The root object representing a category.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Name` | string | Yes | Category name (also used as filename) |
| `Description` | string | No | Optional description |
| `Icon` | string | No | Emoji or icon (default: "??") |
| `Keywords` | string | No | Searchable keywords |
| `TagIds` | string[] | No | List of tag IDs assigned to this category |
| `CreatedDate` | datetime | No | When the category was created |
| `ModifiedDate` | datetime | No | Last modification timestamp |
| `PasswordProtection` | enum | No | `None`, `GlobalPassword`, or `OwnPassword` |
| `OwnPasswordHash` | string | No | BCrypt hash if using own password |
| `SortOrder` | enum | No | Default sort order for children |
| `IsBookmarkImport` | bool | No | True if imported from browser |
| `SourceBrowserType` | enum | No | `Chrome`, `Edge`, `Brave`, `Vivaldi`, `Opera`, `Firefox` |
| `SourceBrowserName` | string | No | Display name of source browser |
| `SourceBookmarksPath` | string | No | Path to browser bookmarks file |
| `LastBookmarkImportDate` | datetime | No | Last import timestamp |
| `ImportedBookmarkCount` | int | No | Number of bookmarks imported |
| `IsBookmarkCategory` | bool | No | True if category contains only URL bookmarks |
| `IsBookmarkLookup` | bool | No | True if included in bookmark search |
| `IsAuditLoggingEnabled` | bool | No | True to enable audit logging |
| `Links` | LinkData[] | No | Array of links in this category |
| `SubCategories` | CategoryData[] | No | Nested subcategories |

---

## LinkData Object

Represents a link, which can be a URL, file, or directory.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Title` | string | Yes | Display title |
| `Url` | string | Yes | URL, file path, or directory path |
| `Description` | string | No | Optional description |
| `Keywords` | string | No | Searchable keywords |
| `TagIds` | string[] | No | List of tag IDs |
| `IsDirectory` | bool | No | True if this is a folder/directory link |
| `CategoryPath` | string | Yes | Path to parent category |
| `CreatedDate` | datetime | No | Creation timestamp |
| `ModifiedDate` | datetime | No | Last modification timestamp |
| `FolderType` | enum | No | `LinkOnly`, `CatalogueFiles`, or `FilteredCatalogue` |
| `FileFilters` | string | No | File extension filters (e.g., "*.jpg;*.png") |
| `IsCatalogEntry` | bool | No | True if this is an auto-generated catalog entry |
| `LastCatalogUpdate` | datetime | No | When catalog was last refreshed |
| `FileSize` | ulong | No | File size in bytes |
| `CatalogFileCount` | int | No | Number of files in subdirectory |
| `CatalogTotalSize` | ulong | No | Total size of files in subdirectory |
| `AutoRefreshCatalog` | bool | No | Auto-refresh catalog on load |
| `IsZipPasswordProtected` | bool | No | True if zip file is password protected |
| `CatalogSortOrder` | enum | No | Sort order for catalog entries |
| `UrlStatus` | enum | No | `Unknown`, `Accessible`, `Error`, or `NotFound` |
| `UrlLastChecked` | datetime | No | When URL was last checked |
| `UrlStatusMessage` | string | No | HTTP status or error message |
| `CatalogEntries` | LinkData[] | No | Child entries for directories |
| `SubLinks` | LinkData[] | No | Child links for URL bookmarks |

---

## Enumerations

### PasswordProtectionType
| Value | Description |
|-------|-------------|
| `None` | No password protection |
| `GlobalPassword` | Uses the global application password |
| `OwnPassword` | Category has its own password |

### SortOption
| Value | Description |
|-------|-------------|
| `NameAscending` | A-Z alphabetical |
| `NameDescending` | Z-A alphabetical |
| `SizeAscending` | Smallest first |
| `SizeDescending` | Largest first |
| `DateAscending` | Oldest first |
| `DateDescending` | Newest first |

### FolderLinkType
| Value | Description |
|-------|-------------|
| `LinkOnly` | Just a folder link, no cataloging |
| `CatalogueFiles` | Catalog all files in directory |
| `FilteredCatalogue` | Catalog files matching filter pattern |

### UrlStatus
| Value | Description |
|-------|-------------|
| `Unknown` | Not checked yet |
| `Accessible` | URL is reachable (2xx/3xx response) |
| `Error` | URL returned an error (5xx) |
| `NotFound` | URL not found (404, DNS failure) |

### BrowserType
| Value | Description |
|-------|-------------|
| `Chrome` | Google Chrome |
| `Edge` | Microsoft Edge |
| `Brave` | Brave Browser |
| `Vivaldi` | Vivaldi |
| `Opera` | Opera |
| `Firefox` | Mozilla Firefox |

---

## Hierarchical Structure

```
CategoryData (root)
??? SubCategories[] (nested CategoryData objects)
?   ??? SubCategories[] (unlimited nesting)
?   ??? Links[]
??? Links[]
    ??? CatalogEntries[] (for directories with FolderType = CatalogueFiles)
    ?   ??? CatalogEntries[] (subdirectory contents, max 2 levels)
    ??? SubLinks[] (for URL bookmarks with child links)
```

---

## Notes

1. **Null Properties**: Properties with null/default values are omitted from the JSON to reduce file size.

2. **Catalog Entries**: When a directory link has `FolderType: CatalogueFiles`, its contents are stored in `CatalogEntries`. These use relative paths for the `Url` field.

3. **Sub-Links**: URL bookmarks can have child links stored in `SubLinks`. This is useful for grouping related URLs.

4. **Encryption**: Categories with password protection are stored as encrypted `.zip.json` files using AES-256.

5. **Date Format**: All dates use ISO 8601 format (e.g., `"2024-01-15T14:30:00"`).

6. **File Naming**: Category names are sanitized for filesystem compatibility (invalid characters removed).

---

## See Also

- [Example Category JSON](examples/sample-category.json) - A comprehensive example file
