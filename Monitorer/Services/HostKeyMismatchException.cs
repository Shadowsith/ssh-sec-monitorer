namespace Monitorer.Services;

/// <summary>The SSH host key differs from the pinned fingerprint.</summary>
public class HostKeyMismatchException : InvalidOperationException
{
    public string Expected { get; }
    public string Received { get; }

    public HostKeyMismatchException(string expected, string received)
        : base($"Host-Key passt nicht! Erwartet {expected}, erhalten {received}.")
    {
        Expected = expected;
        Received = received;
    }
}
