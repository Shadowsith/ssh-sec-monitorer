using System.Collections.Concurrent;
using System.Text;

namespace Monitorer.Services;

/// <summary>
/// Load handling for periodic jobs (agent polls, live checks) - singleton.
/// Every job gets a stable phase offset inside its interval (derived from its key), so with 100 agents and a
/// 60 s interval the fetches are spread evenly over the minute instead of all starting at once - also after a restart.
/// It also prevents a job from running twice at the same time (e.g. a slow SFTP fetch plus "Jetzt abrufen").
/// </summary>
public class PollScheduler
{
    private readonly ConcurrentDictionary<string, DateTime> _running = new();

    /// <summary>Start of the job's current slot: offset + k * interval.</summary>
    public DateTime CurrentSlotStart(string key, int intervalSeconds, DateTime nowUtc)
    {
        var intervalMs = Math.Max(1, intervalSeconds) * 1000L;
        var offsetMs = StableHash(key) % intervalMs;
        var t = (long)(nowUtc - DateTime.UnixEpoch).TotalMilliseconds - offsetMs;
        var slot = t - ((t % intervalMs) + intervalMs) % intervalMs;
        return DateTime.SpecifyKind(DateTime.UnixEpoch.AddMilliseconds(slot + offsetMs), DateTimeKind.Utc);
    }

    /// <summary>Due if it has not run yet in its current slot.</summary>
    public bool IsDue(string key, int intervalSeconds, DateTime? lastRunUtc, DateTime nowUtc) =>
        lastRunUtc is null || lastRunUtc.Value < CurrentSlotStart(key, intervalSeconds, nowUtc);

    /// <summary>When the job will run next (now if it is overdue).</summary>
    public DateTime NextRun(string key, int intervalSeconds, DateTime? lastRunUtc, DateTime nowUtc) =>
        IsDue(key, intervalSeconds, lastRunUtc, nowUtc)
            ? nowUtc
            : CurrentSlotStart(key, intervalSeconds, nowUtc).AddSeconds(Math.Max(1, intervalSeconds));

    public bool IsRunning(string key) => _running.ContainsKey(key);

    /// <summary>Marks the job as running; false if it already runs.</summary>
    public bool TryStart(string key) => _running.TryAdd(key, DateTime.UtcNow);

    public void Finish(string key) => _running.TryRemove(key, out _);

    public int RunningCount => _running.Count;

    public static string AgentKey(int agentId) => $"agent:{agentId}";

    public static string LiveCheckKey(int checkId) => $"livecheck:{checkId}";

    public static string CertCheckKey(int checkId) => $"certcheck:{checkId}";

    /// <summary>FNV-1a - unlike string.GetHashCode() it is the same in every process, so the spread survives restarts.</summary>
    private static long StableHash(string key)
    {
        uint hash = 2166136261;
        foreach (var b in Encoding.UTF8.GetBytes(key))
        {
            hash ^= b;
            hash *= 16777619;
        }
        return hash;
    }
}
