using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Monitorer.Services;

/// <summary>
/// Downloads monitoring.json from an agent host via SFTP (SSH.NET, same port / login as SSH).
/// SFTP allows a file-only SSH user on the host (ForceCommand internal-sftp, chroot, read-only).
/// Host keys are pinned: the SHA256 fingerprint is trusted on first use and must match afterwards.
/// </summary>
public class SshFetchService
{
    private readonly MonitoringSettings _settings;

    public SshFetchService(IOptions<MonitoringSettings> settings) => _settings = settings.Value;

    public async Task<MonitoringFetchResultDto> Download(AgentDb agent, SshSecretsDto secrets, CancellationToken ct)
    {
        var disposables = new List<IDisposable>();
        try
        {
            var (client, fingerprint) = await Connect(agent.Host ?? "", agent.Port ?? 22, agent.HostKeyFingerprint, secrets, disposables, ct);
            using (client)
            {
                var content = await ReadFile(client, agent.RemotePath ?? "", ct);
                client.Disconnect();
                return new MonitoringFetchResultDto { Content = content, HostKeyFingerprint = fingerprint() };
            }
        }
        finally
        {
            foreach (var d in disposables)
                d.Dispose();
        }
    }

    /// <summary>
    /// Step-by-step connection test: host reachable, host key, login, SFTP, optionally the monitoring.json.
    /// Never throws for connection problems - every problem becomes a failed step.
    /// </summary>
    public async Task<SshConnectionTestResultDto> Test(string host, int port, string? expectedFingerprint,
        SshSecretsDto secrets, string? remotePath, Func<string, string?> validateFile, CancellationToken ct)
    {
        var result = new SshConnectionTestResultDto();
        var disposables = new List<IDisposable>();
        Func<string?> fingerprint = () => null;
        try
        {
            SftpClient client;
            try
            {
                (client, fingerprint) = await Connect(host, port, expectedFingerprint, secrets, disposables, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                result.HostKeyFingerprint = fingerprint();
                ExplainConnectError(result, ex, host, port, expectedFingerprint);
                return result;
            }

            using (client)
            {
                result.HostKeyFingerprint = fingerprint();
                result.Add("Host erreichbar", true, $"{host}:{port} antwortet.");
                result.Add("Host-Key", true, expectedFingerprint is null
                    ? $"SHA256:{result.HostKeyFingerprint} – noch nicht gepinnt, wird beim ersten Abruf übernommen."
                    : $"SHA256:{result.HostKeyFingerprint} – stimmt mit dem gepinnten Host-Key überein.");
                result.Add("Anmeldung", true, $"Als {secrets.UserName} angemeldet ({(secrets.AuthType == SshAuthTypes.Key ? "Schlüssel" : "Passwort")}).");
                result.Add("SFTP", true, $"SFTP verfügbar, Startverzeichnis {client.WorkingDirectory}.");

                if (!string.IsNullOrWhiteSpace(remotePath))
                {
                    try
                    {
                        var content = await ReadFile(client, remotePath.Trim(), ct);
                        var fileError = validateFile(content);
                        result.Add("monitoring.json", fileError is null,
                            fileError ?? $"{remotePath} gelesen ({Encoding.UTF8.GetByteCount(content):N0} Bytes), Format gültig.");
                    }
                    catch (SftpPathNotFoundException)
                    {
                        result.Add("monitoring.json", false,
                            $"{remotePath} nicht gefunden. Läuft der Agent? Bei chroot den Pfad relativ zum Chroot angeben.");
                    }
                    catch (SftpPermissionDeniedException)
                    {
                        result.Add("monitoring.json", false, $"Keine Leserechte auf {remotePath} für {secrets.UserName}.");
                    }
                    catch (InvalidOperationException ex)
                    {
                        result.Add("monitoring.json", false, ex.Message);
                    }
                }
                client.Disconnect();
            }
            result.Success = result.Steps.All(s => s.Ok);
            return result;
        }
        finally
        {
            foreach (var d in disposables)
                d.Dispose();
        }
    }

    /// <summary>Returns an error text if the key cannot be read (format / passphrase), otherwise null.</summary>
    public string? ValidatePrivateKey(string privateKey, string? passphrase)
    {
        try
        {
            using var key = LoadPrivateKey(privateKey, passphrase);
            return null;
        }
        catch (Exception ex) when (ex is SshException or ArgumentException or InvalidOperationException)
        {
            return $"Der private Schlüssel kann nicht gelesen werden: {ex.Message}";
        }
    }

    /// <summary>
    /// Public key in authorized_keys format ("ssh-ed25519 AAAA… comment") derived from a private key; null if unreadable.
    /// The key type is read from the SSH wire-format blob itself (RSA keys report several signature algorithms).
    /// </summary>
    public string? DerivePublicKey(string privateKey, string? passphrase, string comment)
    {
        try
        {
            using var key = LoadPrivateKey(privateKey, passphrase);
            var blob = key.HostKeyAlgorithms.First().Data;
            var typeLength = (blob[0] << 24) | (blob[1] << 16) | (blob[2] << 8) | blob[3];
            var type = Encoding.ASCII.GetString(blob, 4, typeLength);
            return $"{type} {Convert.ToBase64String(blob)} {comment}";
        }
        catch (Exception ex) when (ex is SshException or ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Connects (host key check + login); returns the client and a getter for the received fingerprint.</summary>
    private async Task<(SftpClient Client, Func<string?> Fingerprint)> Connect(string host, int port, string? expectedFingerprint,
        SshSecretsDto secrets, List<IDisposable> disposables, CancellationToken ct)
    {
        var methods = CreateAuthenticationMethods(secrets, disposables);
        var info = new Renci.SshNet.ConnectionInfo(host, port, secrets.UserName, methods.ToArray())
        {
            Timeout = TimeSpan.FromSeconds(_settings.SshTimeoutSeconds)
        };

        var client = new SftpClient(info);
        string? fingerprint = null;
        client.HostKeyReceived += (_, e) =>
        {
            fingerprint = e.FingerPrintSHA256;
            e.CanTrust = expectedFingerprint is null || string.Equals(expectedFingerprint, fingerprint, StringComparison.Ordinal);
        };

        try
        {
            await client.ConnectAsync(ct);
        }
        catch (SshConnectionException) when (expectedFingerprint is not null && fingerprint is not null
                                              && fingerprint != expectedFingerprint)
        {
            client.Dispose();
            throw new HostKeyMismatchException(expectedFingerprint, fingerprint);
        }
        catch
        {
            client.Dispose();
            throw;
        }
        return (client, () => fingerprint);
    }

    private async Task<string> ReadFile(SftpClient client, string path, CancellationToken ct)
    {
        using var buffer = new LimitedMemoryStream(_settings.MaxFileBytes);
        await Task.Run(() =>
        {
            // check the size first, so an oversized file is not transferred at all
            var size = client.GetAttributes(path).Size;
            if (size > _settings.MaxFileBytes)
                throw new InvalidOperationException($"monitoring.json ist {size} Bytes groß (max. {_settings.MaxFileBytes}).");
            client.DownloadFile(path, buffer);
        }, ct);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void ExplainConnectError(SshConnectionTestResultDto result, Exception ex, string host, int port,
        string? expectedFingerprint)
    {
        switch (ex)
        {
            case HostKeyMismatchException mismatch:
                result.Add("Host erreichbar", true, $"{host}:{port} antwortet.");
                result.Add("Host-Key", false,
                    $"Host-Key passt nicht! Erwartet SHA256:{mismatch.Expected}, erhalten SHA256:{mismatch.Received}. " +
                    "VM neu installiert? Sonst Vorsicht – möglicher Man-in-the-Middle.");
                break;
            case SshAuthenticationException:
                result.Add("Host erreichbar", true, $"{host}:{port} antwortet.");
                result.Add("Host-Key", true, expectedFingerprint is null ? "noch nicht gepinnt" : "stimmt überein");
                result.Add("Anmeldung", false, $"Anmeldung abgelehnt: {ex.Message} – Benutzer, Schlüssel bzw. authorized_keys prüfen.");
                break;
            case SshConnectionException when ex.Message.Contains("subsystem", StringComparison.OrdinalIgnoreCase):
                result.Add("Host erreichbar", true, $"{host}:{port} antwortet.");
                result.Add("SFTP", false, "SFTP ist auf dem Host nicht verfügbar (Subsystem sftp in sshd_config).");
                break;
            case SocketException or SshOperationTimeoutException or OperationCanceledException:
                result.Add("Host erreichbar", false, $"{host}:{port} nicht erreichbar: {ex.Message}");
                break;
            default:
                result.Add("Verbindung", false, ex.Message);
                break;
        }
    }

    private static List<AuthenticationMethod> CreateAuthenticationMethods(SshSecretsDto secrets, List<IDisposable> disposables)
    {
        var methods = new List<AuthenticationMethod>();
        if (secrets.AuthType == SshAuthTypes.Key && !string.IsNullOrEmpty(secrets.PrivateKey))
        {
            var key = LoadPrivateKey(secrets.PrivateKey, secrets.Passphrase);
            disposables.Add(key);
            var method = new PrivateKeyAuthenticationMethod(secrets.UserName, key);
            disposables.Add(method);
            methods.Add(method);
        }
        if (!string.IsNullOrEmpty(secrets.Password))
        {
            var method = new PasswordAuthenticationMethod(secrets.UserName, secrets.Password);
            disposables.Add(method);
            methods.Add(method);
        }
        if (methods.Count == 0)
            throw new InvalidOperationException("Für die SSH-Zugangsdaten ist kein Schlüssel / Passwort hinterlegt.");
        return methods;
    }

    private static PrivateKeyFile LoadPrivateKey(string privateKey, string? passphrase)
    {
        // normalize line endings - keys pasted from Windows often contain \r\n
        var normalized = privateKey.Replace("\r\n", "\n").Trim() + "\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(normalized));
        return string.IsNullOrEmpty(passphrase) ? new PrivateKeyFile(stream) : new PrivateKeyFile(stream, passphrase);
    }
}
