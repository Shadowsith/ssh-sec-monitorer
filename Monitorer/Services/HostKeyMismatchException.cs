namespace Monitorer.Services;

/// <summary>The SSH host key differs from the pinned fingerprint.</summary>
public class HostKeyMismatchException : InvalidOperationException
{
    public string Expected { get; }
    public string Received { get; }

    public HostKeyMismatchException(string expected, string received, string message)
        : base(message)
    {
        Expected = expected;
        Received = received;
    }
}
