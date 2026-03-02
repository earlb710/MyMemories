using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace MyMemories.Services;

/// <summary>
/// Service for connecting to IMAP servers, browsing folders, fetching messages,
/// searching, and archiving emails to local storage.
/// </summary>
public class ImapEmailService : IDisposable
{
    private ImapClient? _client;
    private bool _disposed;

    /// <summary>
    /// Connects to an IMAP server using the provided account credentials.
    /// </summary>
    public async Task ConnectAsync(EmailAccount account, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync(cancellationToken);

        _client = new ImapClient();

        var secureSocketOptions = account.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;

        await _client.ConnectAsync(
            account.ImapServer,
            account.ImapPort,
            secureSocketOptions,
            cancellationToken);

        var password = EmailCredentialHelper.DecryptPassword(account.EncryptedPassword);
        await _client.AuthenticateAsync(account.Username, password, cancellationToken);
    }

    /// <summary>
    /// Tests connectivity to an IMAP server without keeping the connection open.
    /// Returns null on success or an error message on failure.
    /// </summary>
    public async Task<string?> TestConnectionAsync(EmailAccount account, CancellationToken cancellationToken = default)
    {
        using var testClient = new ImapClient();
        try
        {
            var secureSocketOptions = account.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable;

            await testClient.ConnectAsync(
                account.ImapServer,
                account.ImapPort,
                secureSocketOptions,
                cancellationToken);

            var password = EmailCredentialHelper.DecryptPassword(account.EncryptedPassword);
            await testClient.AuthenticateAsync(account.Username, password, cancellationToken);

            await testClient.DisconnectAsync(true, cancellationToken);
            return null; // Success
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Gets the folder hierarchy from the connected IMAP server.
    /// </summary>
    public async Task<List<EmailFolder>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var personal = _client!.GetFolder(_client.PersonalNamespaces[0]);
        var subfolders = await personal.GetSubfoldersAsync(false, cancellationToken);

        var folders = new List<EmailFolder>();

        // Add INBOX first
        var inbox = _client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
        folders.Add(new EmailFolder
        {
            Name = inbox.Name,
            FullName = inbox.FullName,
            MessageCount = inbox.Count,
            UnreadCount = inbox.Unread
        });
        await inbox.CloseAsync(false, cancellationToken);

        // Add other top-level folders
        foreach (var folder in subfolders)
        {
            if (string.Equals(folder.FullName, "INBOX", StringComparison.OrdinalIgnoreCase))
                continue;

            var emailFolder = await BuildFolderTreeAsync(folder, cancellationToken);
            folders.Add(emailFolder);
        }

        return folders;
    }

    private async Task<EmailFolder> BuildFolderTreeAsync(IMailFolder folder, CancellationToken cancellationToken)
    {
        var emailFolder = new EmailFolder
        {
            Name = folder.Name,
            FullName = folder.FullName
        };

        // Try to open folder to get counts (some folders like [Gmail] can't be opened)
        try
        {
            if (folder.Exists)
            {
                await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
                emailFolder.MessageCount = folder.Count;
                emailFolder.UnreadCount = folder.Unread;
                await folder.CloseAsync(false, cancellationToken);
            }
        }
        catch
        {
            // Folder can't be opened (e.g., virtual folder) - continue without counts
        }

        // Recursively get subfolders
        try
        {
            var subfolders = await folder.GetSubfoldersAsync(false, cancellationToken);
            foreach (var sub in subfolders)
            {
                emailFolder.SubFolders.Add(await BuildFolderTreeAsync(sub, cancellationToken));
            }
        }
        catch
        {
            // Some folders don't support subfolders
        }

        return emailFolder;
    }

    /// <summary>
    /// Gets message summaries (envelopes) from a specific folder.
    /// </summary>
    /// <param name="folderFullName">Full name of the IMAP folder.</param>
    /// <param name="startIndex">Start index (0-based from most recent).</param>
    /// <param name="count">Number of messages to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of message summaries, most recent first.</returns>
    public async Task<List<EmailMessageSummary>> GetMessageSummariesAsync(
        string folderFullName,
        int startIndex,
        int count,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var folder = await GetFolderByNameAsync(folderFullName, cancellationToken);
        await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        var summaries = new List<EmailMessageSummary>();

        if (folder.Count == 0)
        {
            await folder.CloseAsync(false, cancellationToken);
            return summaries;
        }

        // Calculate range (most recent messages first)
        int totalMessages = folder.Count;
        int endIdx = Math.Max(0, totalMessages - 1 - startIndex);
        int startIdx = Math.Max(0, endIdx - count + 1);

        if (startIdx > endIdx)
        {
            await folder.CloseAsync(false, cancellationToken);
            return summaries;
        }

        var items = await folder.FetchAsync(
            startIdx,
            endIdx,
            MessageSummaryItems.UniqueId |
            MessageSummaryItems.Envelope |
            MessageSummaryItems.Flags |
            MessageSummaryItems.Size |
            MessageSummaryItems.BodyStructure,
            cancellationToken);

        foreach (var item in items.Reverse())
        {
            var summary = new EmailMessageSummary
            {
                UniqueId = item.UniqueId.Id,
                Subject = item.Envelope?.Subject ?? "(No Subject)",
                From = FormatAddresses(item.Envelope?.From),
                To = FormatAddresses(item.Envelope?.To),
                Date = item.Envelope?.Date?.LocalDateTime ?? DateTime.MinValue,
                IsRead = item.Flags.HasValue && item.Flags.Value.HasFlag(MessageFlags.Seen),
                HasAttachments = item.BodyStructure is BodyPartMultipart multipart &&
                    multipart.BodyParts.Any(p => p.IsAttachment),
                Size = item.Size ?? 0,
                FolderFullName = folderFullName
            };
            summaries.Add(summary);
        }

        await folder.CloseAsync(false, cancellationToken);
        return summaries;
    }

    /// <summary>
    /// Gets the full details of a single email message.
    /// </summary>
    public async Task<EmailMessageDetail?> GetMessageDetailAsync(
        string folderFullName,
        uint uniqueId,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var folder = await GetFolderByNameAsync(folderFullName, cancellationToken);
        await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        var uid = new UniqueId(uniqueId);
        var message = await folder.GetMessageAsync(uid, cancellationToken);

        if (message == null)
        {
            await folder.CloseAsync(false, cancellationToken);
            return null;
        }

        var detail = new EmailMessageDetail
        {
            UniqueId = uniqueId,
            Subject = message.Subject ?? "(No Subject)",
            From = message.From?.ToString() ?? string.Empty,
            To = message.To?.Select(a => a.ToString()).ToList() ?? new List<string>(),
            Cc = message.Cc?.Select(a => a.ToString()).ToList() ?? new List<string>(),
            Date = message.Date.LocalDateTime,
            TextBody = message.TextBody ?? string.Empty,
            HtmlBody = message.HtmlBody ?? string.Empty,
            FolderFullName = folderFullName
        };

        // Collect attachment info
        int attachmentIndex = 0;
        foreach (var attachment in message.Attachments)
        {
            detail.Attachments.Add(new EmailAttachmentInfo
            {
                FileName = attachment.ContentDisposition?.FileName
                    ?? attachment.ContentType.Name
                    ?? $"attachment_{attachmentIndex}",
                ContentType = attachment.ContentType.MimeType,
                Size = attachment is MimePart part ? (part.Content?.Stream?.Length ?? 0) : 0,
                Index = attachmentIndex
            });
            attachmentIndex++;
        }

        await folder.CloseAsync(false, cancellationToken);
        return detail;
    }

    /// <summary>
    /// Searches emails in a folder using the provided criteria.
    /// </summary>
    public async Task<List<EmailMessageSummary>> SearchAsync(
        string folderFullName,
        EmailSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var folder = await GetFolderByNameAsync(folderFullName, cancellationToken);
        await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        var query = BuildSearchQuery(criteria);
        var uids = await folder.SearchAsync(query, cancellationToken);

        var summaries = new List<EmailMessageSummary>();

        if (uids.Count == 0)
        {
            await folder.CloseAsync(false, cancellationToken);
            return summaries;
        }

        // Fetch summaries for matching UIDs (limit to 200 for performance)
        var limitedUids = uids.Reverse().Take(200).ToList();
        var items = await folder.FetchAsync(
            limitedUids,
            MessageSummaryItems.UniqueId |
            MessageSummaryItems.Envelope |
            MessageSummaryItems.Flags |
            MessageSummaryItems.Size |
            MessageSummaryItems.BodyStructure,
            cancellationToken);

        foreach (var item in items)
        {
            summaries.Add(new EmailMessageSummary
            {
                UniqueId = item.UniqueId.Id,
                Subject = item.Envelope?.Subject ?? "(No Subject)",
                From = FormatAddresses(item.Envelope?.From),
                To = FormatAddresses(item.Envelope?.To),
                Date = item.Envelope?.Date?.LocalDateTime ?? DateTime.MinValue,
                IsRead = item.Flags.HasValue && item.Flags.Value.HasFlag(MessageFlags.Seen),
                HasAttachments = item.BodyStructure is BodyPartMultipart multipart &&
                    multipart.BodyParts.Any(p => p.IsAttachment),
                Size = item.Size ?? 0,
                FolderFullName = folderFullName
            });
        }

        await folder.CloseAsync(false, cancellationToken);

        // Sort by date descending
        summaries.Sort((a, b) => b.Date.CompareTo(a.Date));
        return summaries;
    }

    /// <summary>
    /// Archives (downloads) specified emails as .eml files to a local directory.
    /// </summary>
    public async Task<EmailArchiveResult> ArchiveEmailsAsync(
        string folderFullName,
        IReadOnlyList<uint> uniqueIds,
        string targetDirectory,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        Directory.CreateDirectory(targetDirectory);

        var result = new EmailArchiveResult { TotalRequested = uniqueIds.Count };

        var folder = await GetFolderByNameAsync(folderFullName, cancellationToken);
        await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        foreach (var uid in uniqueIds)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var message = await folder.GetMessageAsync(new UniqueId(uid), cancellationToken);
                if (message == null)
                {
                    result.Failed++;
                    result.Errors.Add($"UID {uid}: Message not found");
                    continue;
                }

                // Generate safe filename from subject and date
                var safeSubject = SanitizeFileName(message.Subject ?? "NoSubject");
                var dateStr = message.Date.LocalDateTime.ToString("yyyy-MM-dd_HHmmss");
                var fileName = $"{dateStr}_{safeSubject}.eml";

                // Ensure unique filename
                var filePath = Path.Combine(targetDirectory, fileName);
                int counter = 1;
                while (File.Exists(filePath))
                {
                    fileName = $"{dateStr}_{safeSubject}_{counter}.eml";
                    filePath = Path.Combine(targetDirectory, fileName);
                    counter++;
                }

                await using var stream = File.Create(filePath);
                await message.WriteToAsync(stream, cancellationToken);

                result.Succeeded++;
                result.SavedFilePaths.Add(filePath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"UID {uid}: {ex.Message}");
            }
        }

        await folder.CloseAsync(false, cancellationToken);
        return result;
    }

    /// <summary>
    /// Archives a single email and also saves its attachments to a subfolder.
    /// </summary>
    public async Task<string?> ArchiveSingleEmailWithAttachmentsAsync(
        string folderFullName,
        uint uniqueId,
        string targetDirectory,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        Directory.CreateDirectory(targetDirectory);

        var folder = await GetFolderByNameAsync(folderFullName, cancellationToken);
        await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        try
        {
            var message = await folder.GetMessageAsync(new UniqueId(uniqueId), cancellationToken);
            if (message == null)
                return null;

            // Save .eml file
            var safeSubject = SanitizeFileName(message.Subject ?? "NoSubject");
            var dateStr = message.Date.LocalDateTime.ToString("yyyy-MM-dd_HHmmss");
            var emlFileName = $"{dateStr}_{safeSubject}.eml";
            var emlPath = Path.Combine(targetDirectory, emlFileName);

            await using (var stream = File.Create(emlPath))
            {
                await message.WriteToAsync(stream, cancellationToken);
            }

            // Save attachments to a subfolder
            if (message.Attachments.Any())
            {
                var attachmentDir = Path.Combine(targetDirectory, $"{dateStr}_{safeSubject}_attachments");
                Directory.CreateDirectory(attachmentDir);

                foreach (var attachment in message.Attachments)
                {
                    var attachmentName = attachment.ContentDisposition?.FileName
                        ?? attachment.ContentType.Name
                        ?? "unnamed_attachment";

                    attachmentName = SanitizeFileName(attachmentName);
                    var attachmentPath = Path.Combine(attachmentDir, attachmentName);

                    if (attachment is MimePart mimePart)
                    {
                        await using var attachmentStream = File.Create(attachmentPath);
                        await mimePart.Content.DecodeToAsync(attachmentStream, cancellationToken);
                    }
                }
            }

            return emlPath;
        }
        finally
        {
            await folder.CloseAsync(false, cancellationToken);
        }
    }

    /// <summary>
    /// Disconnects from the IMAP server.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_client != null && _client.IsConnected)
        {
            await _client.DisconnectAsync(true, cancellationToken);
        }
        _client?.Dispose();
        _client = null;
    }

    /// <summary>
    /// Gets whether the client is currently connected and authenticated.
    /// </summary>
    public bool IsConnected => _client?.IsConnected == true && _client?.IsAuthenticated == true;

    private async Task<IMailFolder> GetFolderByNameAsync(string fullName, CancellationToken cancellationToken)
    {
        if (string.Equals(fullName, "INBOX", StringComparison.OrdinalIgnoreCase))
            return _client!.Inbox;

        return await _client!.GetFolderAsync(fullName, cancellationToken);
    }

    private void EnsureConnected()
    {
        if (_client == null || !_client.IsConnected || !_client.IsAuthenticated)
            throw new InvalidOperationException("Not connected to an IMAP server. Call ConnectAsync first.");
    }

    private static SearchQuery BuildSearchQuery(EmailSearchCriteria criteria)
    {
        var queries = new List<SearchQuery>();

        if (!string.IsNullOrWhiteSpace(criteria.SubjectContains))
            queries.Add(SearchQuery.SubjectContains(criteria.SubjectContains));

        if (!string.IsNullOrWhiteSpace(criteria.FromContains))
            queries.Add(SearchQuery.FromContains(criteria.FromContains));

        if (!string.IsNullOrWhiteSpace(criteria.ToContains))
            queries.Add(SearchQuery.ToContains(criteria.ToContains));

        if (!string.IsNullOrWhiteSpace(criteria.BodyContains))
            queries.Add(SearchQuery.BodyContains(criteria.BodyContains));

        if (criteria.DateAfter.HasValue)
            queries.Add(SearchQuery.DeliveredAfter(criteria.DateAfter.Value));

        if (criteria.DateBefore.HasValue)
            queries.Add(SearchQuery.DeliveredBefore(criteria.DateBefore.Value));

        if (criteria.HasAttachments == true)
            queries.Add(SearchQuery.HasFlags(MessageFlags.None)); // Placeholder; MailKit doesn't have direct attachment search

        if (criteria.IsUnread == true)
            queries.Add(SearchQuery.NotSeen);
        else if (criteria.IsUnread == false)
            queries.Add(SearchQuery.Seen);

        if (queries.Count == 0)
            return SearchQuery.All;

        var combined = queries[0];
        for (int i = 1; i < queries.Count; i++)
        {
            combined = combined.And(queries[i]);
        }
        return combined;
    }

    private static string FormatAddresses(InternetAddressList? addresses)
    {
        if (addresses == null || addresses.Count == 0)
            return string.Empty;

        return string.Join(", ", addresses.Select(a =>
        {
            if (a is MailboxAddress mailbox)
                return !string.IsNullOrEmpty(mailbox.Name) ? $"{mailbox.Name} <{mailbox.Address}>" : mailbox.Address;
            return a.ToString();
        }));
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        // Limit length
        if (sanitized.Length > 80)
            sanitized = sanitized.Substring(0, 80);
        return sanitized.TrimEnd('.', ' ');
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _client?.Dispose();
            _client = null;
            _disposed = true;
        }
    }
}
