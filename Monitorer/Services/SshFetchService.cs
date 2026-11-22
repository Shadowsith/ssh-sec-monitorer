using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Localization;
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
    private readonly IStringLocalizer _l;

    public SshFetchService(IOptions<MonitoringSettings> settings, IStringLocalizer l)
    {
        _settings = settings.Value;
        _l = l;
    }

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
                result.Add(_l["sshTest.stepHost"].Value, true, _l["sshTest.hostOk", host, port].Value);
                result.Add(_l["sshTest.stepHostKey"].Value, true, expectedFingerprint is null
                    ? _l["sshTest.hostKeyNew", result.HostKeyFingerprint ?? ""].Value
                    : _l["sshTest.hostKeyMatch", result.HostKeyFingerprint ?? ""].Value);
                result.Add(_l["sshTest.stepLogin"].Value, true, _l["sshTest.loginOk", secrets.UserName,
                    _l[secrets.AuthType == SshAuthTypes.Key ? "credentials.authKey" : "user.password"].Value].Value);
                result.Add("SFTP", true, _l["sshTest.sftpOk", client.WorkingDirectory].Value);

                if (!string.IsNullOrWhiteSpace(remotePath))
                {
                    try
                    {
                        var content = await ReadFile(client, remotePath.Trim(), ct);
                        var fileError = validateFile(content);
                        result.Add("monitoring.json", fileError is null,
                            fileError ?? _l["sshTest.fileOk", remotePath, Encoding.UTF8.GetByteCount(content).ToString("N0", System.Globalization.CultureInfo.CurrentCulture)].Value);
                    }
                    catch (SftpPathNotFoundException)
                    {
                        result.Add("monitoring.json", false, _l["sshTest.fileNotFound", remotePath].Value);
                    }
                    catch (SftpPermissionDeniedException)
                    {
                        result.Add("monitoring.json", false, _l["sshTest.fileNoPermission", remotePath, secrets.UserName].Value);
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
            return _l["credentials.keyUnreadable", ex.Message].Value;
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
            throw new HostKeyMismatchException(expectedFingerprint, fingerprint,
                _l["sshTest.hostKeyMismatch", expectedFingerprint, fingerprint].Value);
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
        using var buffer = new LimitedMemoryStream(_settings.MaxFileBytes, _l["fetch.fileTooLarge", "monitoring.json", _settings.MaxFileBytes].Value);
        await Task.Run(() =>
        {
            // check the size first, so an oversized file is not transferred at all
            var size = client.GetAttributes(path).Size;
            if (size > _settings.MaxFileBytes)
                throw new InvalidOperationException(_l["fetch.fileTooLarge", "monitoring.json", _settings.MaxFileBytes].Value);
            client.DownloadFile(path, buffer);
        }, ct);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private void ExplainConnectError(SshConnectionTestResultDto result, Exception ex, string host, int port,
        string? expectedFingerprint)
    {
        switch (ex)
        {
            case HostKeyMismatchException mismatch:
                result.Add(_l["sshTest.stepHost"].Value, true, _l["sshTest.hostOk", host, port].Value);
                result.Add(_l["sshTest.stepHostKey"].Value, false, _l["sshTest.hostKeyMismatch", mismatch.Expected, mismatch.Received].Value);
                break;
            case SshAuthenticationException:
                result.Add(_l["sshTest.stepHost"].Value, true, _l["sshTest.hostOk", host, port].Value);
                result.Add(_l["sshTest.stepHostKey"].Value, true, _l[expectedFingerprint is null ? "sshTest.hostKeyNotPinned" : "sshTest.hostKeyMatchShort"].Value);
                result.Add(_l["sshTest.stepLogin"].Value, false, _l["sshTest.loginFailed", ex.Message].Value);
                break;
            case SshConnectionException when ex.Message.Contains("subsystem", StringComparison.OrdinalIgnoreCase):
                result.Add(_l["sshTest.stepHost"].Value, true, _l["sshTest.hostOk", host, port].Value);
                result.Add("SFTP", false, _l["sshTest.sftpUnavailable"].Value);
                break;
            case SocketException or SshOperationTimeoutException or OperationCanceledException:
                result.Add(_l["sshTest.stepHost"].Value, false, _l["sshTest.hostUnreachable", host, port, ex.Message].Value);
                break;
            default:
                result.Add(_l["sshTest.stepConnection"].Value, false, ex.Message);
                break;
        }
    }

    private List<AuthenticationMethod> CreateAuthenticationMethods(SshSecretsDto secrets, List<IDisposable> disposables)
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
            throw new InvalidOperationException(_l["credentials.noSecret"].Value);
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
