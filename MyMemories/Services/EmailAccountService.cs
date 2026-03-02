using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyMemories.Services;

/// <summary>
/// Service for managing email account configuration (CRUD) and credential storage.
/// Accounts are persisted to EmailAccounts.json in the working directory.
/// Passwords are encrypted using DPAPI (Windows Data Protection).
/// </summary>
public class EmailAccountService
{
    private readonly string _accountsFilePath;
    private EmailAccountCollection _collection = new();

    public EmailAccountService(string workingDirectory)
    {
        _accountsFilePath = Path.Combine(workingDirectory, "EmailAccounts.json");
    }

    /// <summary>
    /// Loads email accounts from disk.
    /// </summary>
    public async Task LoadAsync()
    {
        if (File.Exists(_accountsFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_accountsFilePath);
                _collection = JsonSerializer.Deserialize<EmailAccountCollection>(json) ?? new EmailAccountCollection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailAccountService] Failed to load accounts: {ex.Message}");
                _collection = new EmailAccountCollection();
            }
        }
    }

    /// <summary>
    /// Saves email accounts to disk.
    /// </summary>
    public async Task SaveAsync()
    {
        _collection.LastModified = DateTime.Now;
        var json = JsonSerializer.Serialize(_collection, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_accountsFilePath, json);
    }

    /// <summary>
    /// Gets all configured email accounts.
    /// </summary>
    public IReadOnlyList<EmailAccount> GetAccounts() => _collection.Accounts.AsReadOnly();

    /// <summary>
    /// Gets a specific account by ID.
    /// </summary>
    public EmailAccount? GetAccount(string id) => _collection.Accounts.FirstOrDefault(a => a.Id == id);

    /// <summary>
    /// Adds a new email account and saves.
    /// </summary>
    public async Task AddAccountAsync(EmailAccount account)
    {
        _collection.Accounts.Add(account);
        await SaveAsync();
    }

    /// <summary>
    /// Updates an existing email account and saves.
    /// </summary>
    public async Task UpdateAccountAsync(EmailAccount account)
    {
        var existing = _collection.Accounts.FindIndex(a => a.Id == account.Id);
        if (existing >= 0)
        {
            _collection.Accounts[existing] = account;
            await SaveAsync();
        }
    }

    /// <summary>
    /// Removes an email account and saves.
    /// </summary>
    public async Task RemoveAccountAsync(string accountId)
    {
        _collection.Accounts.RemoveAll(a => a.Id == accountId);
        await SaveAsync();
    }

    /// <summary>
    /// Gets pre-configured IMAP settings for known email providers.
    /// </summary>
    public static (string server, int port, bool useSsl) GetProviderSettings(EmailProvider provider)
    {
        return provider switch
        {
            EmailProvider.Gmail => ("imap.gmail.com", 993, true),
            EmailProvider.Outlook => ("outlook.office365.com", 993, true),
            EmailProvider.Yahoo => ("imap.mail.yahoo.com", 993, true),
            EmailProvider.iCloud => ("imap.mail.me.com", 993, true),
            _ => (string.Empty, 993, true)
        };
    }
}

/// <summary>
/// Helper for encrypting/decrypting email passwords using DPAPI.
/// Falls back to base64 encoding on non-Windows or if DPAPI is unavailable.
/// </summary>
public static class EmailCredentialHelper
{
    /// <summary>
    /// Encrypts a password for storage.
    /// </summary>
    public static string EncryptPassword(string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword))
            return string.Empty;

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainPassword);
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch
        {
            // Fallback: simple base64 (not secure, but functional)
            return "B64:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plainPassword));
        }
    }

    /// <summary>
    /// Decrypts a stored password.
    /// </summary>
    public static string DecryptPassword(string encryptedPassword)
    {
        if (string.IsNullOrEmpty(encryptedPassword))
            return string.Empty;

        // Check for base64 fallback
        if (encryptedPassword.StartsWith("B64:"))
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encryptedPassword.Substring(4)));
        }

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedPassword);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // If decryption fails, return empty
            return string.Empty;
        }
    }
}
