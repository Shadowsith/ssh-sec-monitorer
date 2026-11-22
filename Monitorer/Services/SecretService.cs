using Microsoft.AspNetCore.DataProtection;

namespace Monitorer.Services;

/// <summary>
/// Encrypts / decrypts secrets (SSH keys, passwords) before they are stored in the database.
/// Uses ASP.NET Data Protection - the key ring lives in DataProtection:KeyPath (Docker volume).
/// </summary>
public class SecretService
{
    private readonly IDataProtector _protector;

    public SecretService(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("Monitorer.Secrets.v1");

    /// <summary>Encrypts a value; empty values become null.</summary>
    public string? Protect(string? plain) => string.IsNullOrEmpty(plain) ? null : _protector.Protect(plain);

    /// <summary>Decrypts a stored value; null stays null.</summary>
    public string? Unprotect(string? protectedValue) =>
        string.IsNullOrEmpty(protectedValue) ? null : _protector.Unprotect(protectedValue);
}
