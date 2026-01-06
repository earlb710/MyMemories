using System;
using System.Security.Cryptography;
using System.Text;

namespace MyMemories.Utilities;

/// <summary>
/// Utilities for password hashing and verification.
/// </summary>
public static class PasswordUtilities
{
    /// <summary>
    /// Hashes a password using SHA256.
    /// </summary>
    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return string.Empty;

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verifies a password against a stored hash.
    /// </summary>
    public static bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
            return false;

        var computedHash = HashPassword(password);
        return computedHash == hash;
    }
}