using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyMemories;

/// <summary>
/// Represents a configured IMAP email account.
/// </summary>
public class EmailAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DisplayName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string ImapServer { get; set; } = string.Empty;
    public int ImapPort { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted/stored password (app password for Gmail).
    /// Stored using DPAPI protection.
    /// </summary>
    public string EncryptedPassword { get; set; } = string.Empty;

    /// <summary>
    /// Provider hint for pre-filling server settings.
    /// </summary>
    public EmailProvider Provider { get; set; } = EmailProvider.Other;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? LastConnectedDate { get; set; }

    public override string ToString() => !string.IsNullOrEmpty(DisplayName) ? DisplayName : EmailAddress;
}

/// <summary>
/// Known email providers with pre-configured IMAP settings.
/// </summary>
public enum EmailProvider
{
    Gmail,
    Outlook,
    Yahoo,
    iCloud,
    Other
}

/// <summary>
/// Represents an IMAP mail folder.
/// </summary>
public class EmailFolder
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public int UnreadCount { get; set; }
    public List<EmailFolder> SubFolders { get; set; } = new();

    public override string ToString() => Name;
}

/// <summary>
/// Represents an email message summary (envelope) for listing.
/// </summary>
public class EmailMessageSummary
{
    public uint UniqueId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public bool IsRead { get; set; }
    public bool HasAttachments { get; set; }
    public long Size { get; set; }

    /// <summary>
    /// The folder this message belongs to.
    /// </summary>
    [JsonIgnore]
    public string FolderFullName { get; set; } = string.Empty;

    public override string ToString() => Subject;
}

/// <summary>
/// Represents a full email message with body and attachments.
/// </summary>
public class EmailMessageDetail
{
    public uint UniqueId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public List<string> To { get; set; } = new();
    public List<string> Cc { get; set; } = new();
    public DateTime Date { get; set; }
    public string TextBody { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public List<EmailAttachmentInfo> Attachments { get; set; } = new();
    public string FolderFullName { get; set; } = string.Empty;

    public override string ToString() => Subject;
}

/// <summary>
/// Attachment metadata for an email.
/// </summary>
public class EmailAttachmentInfo
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }

    /// <summary>
    /// Index of this attachment in the MimeMessage for retrieval.
    /// </summary>
    public int Index { get; set; }
}

/// <summary>
/// Search criteria for email searching.
/// </summary>
public class EmailSearchCriteria
{
    public string? SubjectContains { get; set; }
    public string? FromContains { get; set; }
    public string? ToContains { get; set; }
    public string? BodyContains { get; set; }
    public DateTime? DateAfter { get; set; }
    public DateTime? DateBefore { get; set; }
    public bool? HasAttachments { get; set; }
    public bool? IsUnread { get; set; }
}

/// <summary>
/// Result of archiving emails locally.
/// </summary>
public class EmailArchiveResult
{
    public int TotalRequested { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<string> SavedFilePaths { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Container for persisting email accounts.
/// </summary>
public class EmailAccountCollection
{
    public List<EmailAccount> Accounts { get; set; } = new();
    public DateTime LastModified { get; set; } = DateTime.Now;
}
