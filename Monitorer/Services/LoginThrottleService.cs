using System.Collections.Concurrent;

namespace Monitorer.Services;

/// <summary>
/// Simple brute-force protection (singleton): after too many failed logins for a user name / IP
/// combination, further attempts are blocked for a while.
/// </summary>
public class LoginThrottleService
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, (int Failures, DateTime LastFailureUtc)> _failures = new();

    public bool IsLocked(string userName, string? remoteIp)
    {
        var key = Key(userName, remoteIp);
        if (!_failures.TryGetValue(key, out var entry))
            return false;
        if (DateTime.UtcNow - entry.LastFailureUtc > LockDuration)
        {
            _failures.TryRemove(key, out _);
            return false;
        }
        return entry.Failures >= MaxFailures;
    }

    public void RegisterFailure(string userName, string? remoteIp) =>
        _failures.AddOrUpdate(Key(userName, remoteIp),
            _ => (1, DateTime.UtcNow),
            (_, e) => (e.Failures + 1, DateTime.UtcNow));

    public void RegisterSuccess(string userName, string? remoteIp) =>
        _failures.TryRemove(Key(userName, remoteIp), out _);

    private static string Key(string userName, string? remoteIp) =>
        $"{userName.Trim().ToLowerInvariant()}|{remoteIp}";
}
