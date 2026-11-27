using System.Text;
using Monitorer.Data;

namespace Monitorer.Services;

/// <summary>Protocol answer that prevents the TLS upgrade; Key is a translation key ("certChecks.startTls.*").</summary>
public class StartTlsException : Exception
{
    public string Key { get; }
    public object[] Args { get; }

    public StartTlsException(string key, params object[] args) : base(key)
    {
        Key = key;
        Args = args;
    }
}

/// <summary>
/// Asks a plain-text server to switch to TLS (STARTTLS) on an open connection. Afterwards the caller starts the
/// TLS handshake on the same stream. Lines are read byte by byte, so nothing of the handshake is swallowed.
/// </summary>
public static class StartTlsNegotiator
{
    private const int MaxLineBytes = 4096;
    private const int MaxReplyLines = 100;
    private const int MaxLdapResponseBytes = 64 * 1024;

    /// <summary>LDAPMessage { messageID 1, ExtendedRequest { requestName "1.3.6.1.4.1.1466.20037" } } (BER).</summary>
    private static readonly byte[] LdapStartTlsRequest =
        [0x30, 0x1d, 0x02, 0x01, 0x01, 0x77, 0x18, 0x80, 0x16, .. "1.3.6.1.4.1.1466.20037"u8];

    public static Task Negotiate(Stream stream, string protocol, CancellationToken ct) => protocol switch
    {
        CertCheckProtocols.Smtp => Smtp(stream, ct),
        CertCheckProtocols.Imap => Imap(stream, ct),
        CertCheckProtocols.Ldap => Ldap(stream, ct),
        _ => Task.CompletedTask
    };

    private static async Task Smtp(Stream stream, CancellationToken ct)
    {
        var (code, _) = await ReadSmtpReply(stream, ct);
        if (code != 220)
            throw new StartTlsException("certChecks.startTls.unexpected", "SMTP", code);
        await Write(stream, "EHLO ssm-monitorer\r\n", ct);
        var (ehlo, lines) = await ReadSmtpReply(stream, ct);
        if (ehlo != 250)
            throw new StartTlsException("certChecks.startTls.unexpected", "SMTP EHLO", ehlo);
        if (!lines.Any(l => l.Length > 4 && l[4..].Trim().StartsWith("STARTTLS", StringComparison.OrdinalIgnoreCase)))
            throw new StartTlsException("certChecks.startTls.notOffered", "SMTP");
        await Write(stream, "STARTTLS\r\n", ct);
        var (start, _) = await ReadSmtpReply(stream, ct);
        if (start != 220)
            throw new StartTlsException("certChecks.startTls.rejected", "SMTP", start);
    }

    /// <summary>Reads "250-..." lines up to the last "250 ..." line.</summary>
    private static async Task<(int Code, List<string> Lines)> ReadSmtpReply(Stream stream, CancellationToken ct)
    {
        var lines = new List<string>();
        while (lines.Count < MaxReplyLines)
        {
            var line = await ReadLine(stream, ct);
            if (line.Length < 3 || !int.TryParse(line[..3], out var code))
                throw new StartTlsException("certChecks.startTls.invalidReply", "SMTP");
            lines.Add(line);
            if (line.Length == 3 || line[3] != '-')
                return (code, lines);
        }
        throw new StartTlsException("certChecks.startTls.invalidReply", "SMTP");
    }

    private static async Task Imap(Stream stream, CancellationToken ct)
    {
        var greeting = await ReadLine(stream, ct);
        if (!greeting.StartsWith("* OK", StringComparison.OrdinalIgnoreCase))
            throw new StartTlsException("certChecks.startTls.unexpectedText", "IMAP", Shorten(greeting));
        await Write(stream, "a1 STARTTLS\r\n", ct);
        for (var i = 0; i < MaxReplyLines; i++)
        {
            var line = await ReadLine(stream, ct);
            if (!line.StartsWith("a1 ", StringComparison.Ordinal))
                continue;   // untagged lines, e.g. "* CAPABILITY ..."
            if (line.StartsWith("a1 OK", StringComparison.OrdinalIgnoreCase))
                return;
            throw new StartTlsException("certChecks.startTls.rejectedText", "IMAP", Shorten(line));
        }
        throw new StartTlsException("certChecks.startTls.invalidReply", "IMAP");
    }

    private static async Task Ldap(Stream stream, CancellationToken ct)
    {
        await stream.WriteAsync(LdapStartTlsRequest, ct);
        await stream.FlushAsync(ct);

        // LDAPMessage SEQUENCE: tag + length, then the content
        if (await ReadByte(stream, ct) != 0x30)
            throw new StartTlsException("certChecks.startTls.invalidReply", "LDAP");
        var length = await ReadBerLength(stream, ct);
        if (length is < 1 or > MaxLdapResponseBytes)
            throw new StartTlsException("certChecks.startTls.invalidReply", "LDAP");
        var content = new byte[length];
        await stream.ReadExactlyAsync(content, ct);

        // content: messageID INTEGER, ExtendedResponse [APPLICATION 24] { resultCode ENUMERATED, ... }
        var pos = 0;
        SkipTlv(content, ref pos);
        if (pos >= content.Length || content[pos++] != 0x78)
            throw new StartTlsException("certChecks.startTls.invalidReply", "LDAP");
        ReadBerLength(content, ref pos);
        if (pos + 3 > content.Length || content[pos] != 0x0a || content[pos + 1] != 0x01)
            throw new StartTlsException("certChecks.startTls.invalidReply", "LDAP");
        var resultCode = content[pos + 2];
        if (resultCode != 0)
            throw new StartTlsException("certChecks.startTls.rejected", "LDAP", resultCode);
    }

    private static async Task<int> ReadBerLength(Stream stream, CancellationToken ct)
    {
        var first = await ReadByte(stream, ct);
        if (first < 0x80)
            return first;
        var count = first & 0x7f;
        if (count is 0 or > 3)
            throw new StartTlsException("certChecks.startTls.invalidReply", "LDAP");
        var length = 0;
        for (var i = 0; i < count; i++)
            length = (length << 8) | await ReadByte(stream, ct);
        return length;
    }

    private static int ReadBerLength(byte[] data, ref int pos)
    {
        if (pos >= data.Length)
            throw new StartTlsException("certChecks.startTls.invalidReply", "LDAP");
        var first = data[pos++];
        if (first < 0x80)
            return first;
        var count = first & 0x7f;
        if (count is 0 or > 3 || pos + count > data.Length)
            throw new StartTlsException("certChecks.startTls.invalidReply", "LDAP");
        var length = 0;
        for (var i = 0; i < count; i++)
            length = (length << 8) | data[pos++];
        return length;
    }

    private static void SkipTlv(byte[] data, ref int pos)
    {
        pos++; // tag
        var length = ReadBerLength(data, ref pos);
        pos += length;
        if (pos > data.Length)
            throw new StartTlsException("certChecks.startTls.invalidReply", "LDAP");
    }

    private static async Task<int> ReadByte(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[1];
        if (await stream.ReadAsync(buffer, ct) == 0)
            throw new StartTlsException("certChecks.startTls.closed");
        return buffer[0];
    }

    private static async Task<string> ReadLine(Stream stream, CancellationToken ct)
    {
        var bytes = new List<byte>();
        while (bytes.Count < MaxLineBytes)
        {
            var b = await ReadByte(stream, ct);
            if (b == '\n')
                return Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
            bytes.Add((byte)b);
        }
        throw new StartTlsException("certChecks.startTls.invalidReply", "line");
    }

    private static async Task Write(Stream stream, string text, CancellationToken ct)
    {
        await stream.WriteAsync(Encoding.ASCII.GetBytes(text), ct);
        await stream.FlushAsync(ct);
    }

    private static string Shorten(string text) => text.Length <= 120 ? text : text[..120] + "…";
}
