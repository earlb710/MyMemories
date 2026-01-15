# MyMemories - Fonts and Icons Reference Guide

**Last Updated:** 2026-01-15  
**Application:** MyMemories File Viewer

## ?? Overview

This document provides a comprehensive reference for all fonts and icons used in the MyMemories application. All icons use **Segoe MDL2 Assets** font, which is the standard icon font for Windows applications.

---

## ?? Standard Fonts

### Primary Font: Segoe UI
- **Used for:** All text content, labels, buttons
- **Default Size:** 14pt
- **Weight Variants:**
  - Regular (default)
  - SemiBold (headings, selected items)
  - Bold (emphasis)

### Icon Font: Segoe MDL2 Assets
- **Used for:** All system icons throughout the application
- **Default Size:** 16pt (varies by context)
- **Format:** Unicode glyphs (&#xE000; format)

---

## ?? TreeView Icons

### Category Icons (User-Defined)
- **Format:** Text-based emoji or single characters
- **Size:** 16pt
- **Examples:**
  - ?? Folder emoji
  - ?? Book emoji
  - ?? House emoji
  - ?? Briefcase emoji
  - Custom user text characters

### Special System Nodes

#### Archive Node
- **Icon:** `A` (red colored)
- **Color:** `Colors.Red` (#FF0000)
- **Purpose:** Archived categories container

#### Divider Node
- **Icon:** None (empty string)
- **Name:** `———————————————————` (em dashes)
- **Purpose:** Visual separator before Archive

### Link Type Indicators

#### URL Links
- **Badge:** Blue dot (right side)
- **Status Colors:**
  - Green: Accessible
  - Yellow: Redirect detected
  - Orange: Moved permanently
  - Red: Not accessible

#### Password Protected
- **Icon:** `&#xE72E;` ?? (Lock)
- **Size:** 7pt
- **Color:** Gold (#FFD700)
- **Position:** Top-right corner of category/link icon

### Category Badges

#### Rating Star
- **Icon:** `&#xE735;` ? (Star)
- **Size:** 12pt
- **Color:** Amber (#FFC107)
- **Position:** Before text label

#### Tag Indicators
- **Icon:** `&#xE8EC;` ??? (Tag)
- **Size:** 10pt
- **Color:** User-defined per tag
- **Position:** Before text label (multiple badges possible)

#### Changed Folder Warning
- **Icon:** `&#xE7BA;` ?? (Warning)
- **Size:** 8pt
- **Color:** System Critical (red)
- **Position:** Top-right corner

---

## ?? Menu Bar Icons

### File Menu
| Menu Item | Icon Code | Icon | Description |
|-----------|-----------|------|-------------|
| Import Browser Bookmarks | `&#xE8A7;` | ?? | Download/Import |
| Import Category Operations | `&#xE8B5;` | ?? | Document List |
| Export Bookmarks | `&#xEDE1;` | ?? | Upload/Export |
| Exit | `&#xE7E8;` | ? | Close/Exit |

### Config Menu
| Menu Item | Icon Code | Icon | Description |
|-----------|-----------|------|-------------|
| Directory Setup | `&#xE8B7;` | ?? | Folder Open |
| Security Setup | `&#xE72E;` | ?? | Lock/Security |
| Tag Management | `&#xE8EC;` | ??? | Tag |
| Rating Management | `&#xE735;` | ? | Star |
| Options | `&#xE713;` | ?? | Settings |

---

## ??? Context Menu Icons

### Category Context Menu
| Menu Item | Icon Code | Icon | Description |
|-----------|-----------|------|-------------|
| Add Link | Built-in `Add` | ? | Add/Plus |
| Add Sub Category | `&#xE8F4;` | ??+ | New Folder |
| Edit Category | Built-in `Edit` | ?? | Edit/Pencil |
| Change Password | `&#xE72E;` | ?? | Lock |
| Backup Directories | `&#xE8F1;` | ?? | Save/Backup |
| Add Tag | `&#xE8EC;` | ??? | Tag |
| Remove Tag | `&#xE738;` | ??? | Remove/Cancel |
| Ratings | `&#xE735;` | ? | Star |
| Zip Category | `&#xE7B8;` | ??? | Compress/Archive |
| Remove Category | Built-in `Delete` | ??? | Delete |
| Stats | `&#xE9D9;` | ?? | Chart/Statistics |
| Sort By | `&#xE8CB;` | ?? | Sort |

### Link Context Menu
| Menu Item | Icon Code | Icon | Description |
|-----------|-----------|------|-------------|
| Add Sub-Link | `&#xE710;` | ?? | Link |
| Move Link | `&#xE8DE;` | ?? | Move/Transfer |
| Edit Link | Built-in `Edit` | ?? | Edit/Pencil |
| Copy Link | Built-in `Copy` | ?? | Copy |
| Change Password | `&#xE72E;` | ?? | Lock |
| Backup Zip | `&#xE8F1;` | ?? | Save/Backup |
| Add Tag | `&#xE8EC;` | ??? | Tag |
| Remove Tag | `&#xE738;` | ??? | Remove/Cancel |
| Ratings | `&#xE735;` | ? | Star |
| Summarize URL | `&#xE8F4;` | ?? | Document |
| Explore Here | `&#xE8DA;` | ?? | Folder Explore |
| Zip Folder | `&#xE7B8;` | ??? | Compress/Archive |
| Remove Link | Built-in `Delete` | ??? | Delete |
| Sort Catalog | `&#xE8CB;` | ?? | Sort |

---

## ?? Tag Icons

### Tag Badge Display
- **Icon:** `&#xE8EC;` ??? (Tag glyph)
- **Size:** 10pt (in tree), 14pt (in dialogs)
- **Background:** Colored circle with user-defined color
- **Foreground:** White (for contrast)
- **Shape:** Circular badge

### Common Tag Colors
| Tag Name | Color Code | Color |
|----------|-----------|-------|
| Important | `#FF4444` | Red |
| Work | `#4169E1` | Royal Blue |
| Personal | `#32CD32` | Lime Green |
| Urgent | `#FF8C00` | Dark Orange |
| Archive | `#808080` | Gray |

---

## ?? Dialog Icons

### Category Dialog
| Section | Icon Code | Icon | Description |
|---------|-----------|------|-------------|
| Name Field Icon | User-defined | ?? | Category icon |
| Password | `&#xE72E;` | ?? | Lock |
| Directory Browse | `&#xE8DA;` | ?? | Folder |

### Link Dialog
| Section | Icon Code | Icon | Description |
|---------|-----------|------|-------------|
| Type: File | `&#xE8A5;` | ?? | Document |
| Type: Folder | `&#xE8B7;` | ?? | Folder |
| Type: URL | `&#xE71B;` | ?? | Link |
| Type: Text | `&#xE8A5;` | ?? | Text |
| Type: Archive | `&#xE7B8;` | ??? | Zip Archive |
| Browse | `&#xE8DA;` | ?? | Folder Open |
| Password | `&#xE72E;` | ?? | Lock |

### Tag Management Dialog
| Button | Icon Code | Icon | Description |
|--------|-----------|------|-------------|
| Add Tag | `&#xE710;` | ? | Add |
| Edit Tag | `&#xE70F;` | ?? | Edit |
| Delete Tag | `&#xE74D;` | ??? | Delete |
| Color Picker | `&#xE790;` | ?? | Palette |

### Rating Management Dialog
| Button | Icon Code | Icon | Description |
|--------|-----------|------|-------------|
| Add Rating | `&#xE710;` | ? | Add |
| Edit Rating | `&#xE70F;` | ?? | Edit |
| Delete Rating | `&#xE74D;` | ??? | Delete |
| Star Icon | `&#xE735;` | ? | Star |

---

## ?? Tab Icons

### Details TabView

#### Summary Tab
- **Icon:** `&#xE8F2;` ?? (Info/Document)
- **Label:** "Summary"
- **Purpose:** Category/Link details and metadata

#### Content Tab
- **Icon:** `&#xE8A5;` ?? (File/Content)
- **Label:** "Content"
- **Purpose:** File preview, text, images, web content

---

## ?? Button Icons

### TreeView Action Buttons
| Button | Icon Code | Icon | Description |
|--------|-----------|------|-------------|
| Create Category | `&#xE8F4;` | ??+ | New Folder |
| Add Link | `&#xE71B;` | ?? | Link |
| Search | `&#xE721;` | ?? | Search |
| Tag Filter | `&#xE8EC;` | ??? | Tag |

### Content View Buttons
| Button | Icon Code | Icon | Description |
|--------|-----------|------|-------------|
| Go/Load URL | `&#xE72C;` | ?? | Forward/Go |
| Refresh | `&#xE72C;` | ?? | Refresh |
| Open in Explorer | `&#xE8DA;` | ?? | Folder |

---

## ?? Icon Usage Guidelines

### Size Recommendations
| Context | Size | Usage |
|---------|------|-------|
| Menu Items | 16pt | Standard menu icons |
| Tree Icons | 16pt | Category/link icons |
| Badges | 7-10pt | Small indicators |
| Buttons | 16pt | Action buttons |
| Dialogs | 20pt | Dialog headers |
| Status | 12pt | Status indicators |

### Color Standards
| Element | Color | Usage |
|---------|-------|-------|
| Icons (Light Theme) | Black | Standard icons |
| Icons (Dark Theme) | White | Standard icons |
| Password Badge | Gold (#FFD700) | Password indicator |
| Rating Star | Amber (#FFC107) | Ratings |
| Warning | Red | Errors/warnings |
| Success | Green | Success states |
| Archive Icon | Red (#FF0000) | Archive node |
| URL Status - OK | Green | Accessible URLs |
| URL Status - Redirect | Yellow/Orange | Redirects |
| URL Status - Error | Red | Failed URLs |

---

## ?? Font Family Reference

### Segoe MDL2 Assets Glyph Map

**Common Icon Categories:**

#### Navigation & Actions
- `&#xE700;` - GlobalNavButton
- `&#xE710;` - Add
- `&#xE711;` - Remove
- `&#xE721;` - Search
- `&#xE72C;` - Forward/Go
- `&#xE72E;` - Lock/Security
- `&#xE735;` - FavoriteStar
- `&#xE738;` - Cancel/Remove
- `&#xE74D;` - Delete
- `&#xE70F;` - Edit/Pencil
- `&#xE713;` - Settings

#### Files & Folders
- `&#xE8A5;` - Document/File
- `&#xE8A7;` - Download/Import
- `&#xE8B5;` - List/Documents
- `&#xE8B7;` - Folder/Open
- `&#xE8CB;` - Sort
- `&#xE8DA;` - FolderExplore
- `&#xE8EC;` - Tag
- `&#xE8F1;` - Save/Backup
- `&#xE8F4;` - NewFolder

#### Archive & Compress
- `&#xE7B8;` - Archive/Zip

#### Communication
- `&#xE71B;` - Link

#### Status & Info
- `&#xE7BA;` - Warning
- `&#xE9D9;` - Chart/Statistics
- `&#xEDE1;` - Upload/Export
- `&#xE7E8;` - Cancel/Close

---

## ?? Consistency Checklist

When adding new icons to the application:

- ? Use **Segoe MDL2 Assets** font family
- ? Use **16pt** as default size (adjust by context)
- ? Use **Unicode glyph format** (`&#xE000;`)
- ? Maintain **color consistency** with theme
- ? Add entry to this documentation
- ? Use **semantic naming** (e.g., "Lock" not "E72E")
- ? Test in both **Light and Dark themes**
- ? Ensure **accessibility** (icons have tooltips/labels)

---

## ?? Adding New Icons

### Step-by-Step Guide

1. **Find the Icon:**
   - Use [Microsoft Segoe MDL2 Assets reference](https://docs.microsoft.com/en-us/windows/apps/design/style/segoe-ui-symbol-font)
   - Or use Character Map (`charmap.exe`) and select "Segoe MDL2 Assets"

2. **Get the Unicode:**
   - Note the hex code (e.g., `E8EC`)
   - Format as `&#xE8EC;`

3. **Add to XAML:**
   ```xml
   <FontIcon Glyph="&#xE8EC;" FontSize="16"/>
   ```

4. **Add to C# (if needed):**
   ```csharp
   var icon = new FontIcon
   {
       Glyph = "\uE8EC",
       FontFamily = new FontFamily("Segoe MDL2 Assets"),
       FontSize = 16
   };
   ```

5. **Update This Document:**
   - Add to appropriate section
   - Include icon code, description, and usage

---

## ?? Troubleshooting

### Icons Not Displaying
- **Problem:** Icons show as `?` or empty boxes
- **Solution:** Ensure using `Segoe MDL2 Assets` font
- **Fallback:** Use simple ASCII characters (A, *, -, etc.)

### Emoji Not Rendering
- **Problem:** Emoji characters show as `??`
- **Solution:** Use Segoe MDL2 icons instead of emoji
- **Alternative:** Use simple text characters

### Color Not Applying
- **Problem:** Icon color incorrect
- **Solution:** Set `Foreground` property explicitly
- **Check:** Theme-aware colors for light/dark mode

---

## ?? External References

- [Microsoft Segoe MDL2 Assets](https://docs.microsoft.com/en-us/windows/apps/design/style/segoe-ui-symbol-font)
- [WinUI 3 Icon Guidelines](https://docs.microsoft.com/en-us/windows/apps/design/style/icons)
- [Windows Icon Design Guidelines](https://docs.microsoft.com/en-us/windows/apps/design/style/iconography/app-icon-design)

---

## ?? Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-15 | Initial documentation created |

---

**Note:** This document should be updated whenever new icons are added or font usage changes in the application.
