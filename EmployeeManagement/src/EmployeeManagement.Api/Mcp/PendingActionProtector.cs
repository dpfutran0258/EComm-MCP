using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using EmployeeManagement.Application.AI;

namespace EmployeeManagement.Api.Mcp;

/// <summary>
/// Confirmation tokens built on ASP.NET Core Data Protection: encrypted, tamper-proof and time-limited.
/// A token only exists if this server created it for a destructive tool call, so a client cannot invent one.
/// </summary>
public sealed class PendingActionProtector : IPendingActionProtector
{
    /// <summary>How long the user has to confirm.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly ITimeLimitedDataProtector _protector;
    private readonly ILogger<PendingActionProtector> _logger;

    public PendingActionProtector(IDataProtectionProvider provider, ILogger<PendingActionProtector> logger)
    {
        _protector = provider.CreateProtector("EmployeeManagement.Chat.PendingAction.v1").ToTimeLimitedDataProtector();
        _logger = logger;
    }

    public string Protect(PendingAction action, out DateTime expiresAtUtc)
    {
        var expires = DateTimeOffset.UtcNow.Add(Lifetime);
        expiresAtUtc = expires.UtcDateTime;
        return _protector.Protect(JsonSerializer.Serialize(action), expires);
    }

    public PendingAction? Unprotect(string token)
    {
        try
        {
            var json = _protector.Unprotect(token);
            return JsonSerializer.Deserialize<PendingAction>(json);
        }
        catch (CryptographicException ex)
        {
            // Expired, tampered with, or produced by a different key ring.
            _logger.LogWarning("Rejected a confirmation token: {Reason}", ex.Message);
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
