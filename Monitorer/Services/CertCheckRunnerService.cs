using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>
/// Executes certificate checks: TLS handshake with host:port (directly or after STARTTLS for SMTP / IMAP / LDAP),
/// reads validity of the server certificate.
/// Untrusted certificates (self-signed, internal CA) are read as well - the validation result is only reported.
/// </summary>
public class CertCheckRunnerService
{
    private readonly CertCheckRepository _repo;
    private readonly PollScheduler _scheduler;
    private readonly LiveUpdateService _live;
    private readonly MonitoringSettings _settings;
    private readonly IStringLocalizer _l;
    private readonly ILogger<CertCheckRunnerService> _logger;

    public CertCheckRunnerService(CertCheckRepository repo, PollScheduler scheduler, LiveUpdateService live,
        IOptions<MonitoringSettings> settings, IStringLocalizer l, ILogger<CertCheckRunnerService> logger)
    {
        _repo = repo;
        _scheduler = scheduler;
        _live = live;
        _settings = settings.Value;
        _l = l;
        _logger = logger;
    }

    public async Task RunDue(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var due = (await _repo.SelectEnabled())
            .Where(c => !_scheduler.IsRunning(PollScheduler.CertCheckKey(c.Id))
                        && _scheduler.IsDue(PollScheduler.CertCheckKey(c.Id), c.IntervalSeconds, c.LastCheckedAt, now))
            .OrderBy(c => c.LastCheckedAt ?? DateTime.MinValue)
            .ToList();
        await Parallel.ForEachAsync(due, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
            async (check, token) => await RunGuarded(check, token));
    }

    /// <summary>POST api/cert-checks/{id}/run.</summary>
    public async Task<ServiceResult<CertCheckDto>> RunNow(int id, CancellationToken ct)
    {
        var check = await _repo.SelectById(id);
        if (check is null)
            return ServiceResult<CertCheckDto>.NotFound(_l["certChecks.notFound", id].Value);
        if (!await RunGuarded(check, ct))
            return ServiceResult<CertCheckDto>.Locked(_l["certChecks.running"].Value);
        return ServiceResult<CertCheckDto>.Ok(new CertCheckDto(check));
    }

    private async Task<bool> RunGuarded(CertCheckDb check, CancellationToken ct)
    {
        var key = PollScheduler.CertCheckKey(check.Id);
        if (!_scheduler.TryStart(key))
            return false;
        try
        {
            await RunAndStore(check, ct);
            await _live.Publish(LiveUpdateService.CertChecks);
            return true;
        }
        finally
        {
            _scheduler.Finish(key);
        }
    }

    private async Task RunAndStore(CertCheckDb check, CancellationToken ct)
    {
        var previous = check.LastStatus;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(check.TimeoutSeconds));
        try
        {
            var (cert, errors) = await ReadCertificate(check, timeout.Token);
            check.NotBefore = cert.NotBefore.ToUniversalTime();
            check.NotAfter = cert.NotAfter.ToUniversalTime();
            check.Subject = cert.Subject;
            check.Issuer = cert.Issuer;
            check.Thumbprint = cert.Thumbprint;
            check.ChainValid = errors == SslPolicyErrors.None;
            (check.LastStatus, check.LastMessage) = Evaluate(check, errors, DateTime.UtcNow);
        }
        catch (StartTlsException ex) when (!ct.IsCancellationRequested)
        {
            check.LastStatus = MonitorStatus.Down;
            check.LastMessage = _l[ex.Key, ex.Args].Value;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // keep the last known certificate data: an outage must not resolve an expiry alert
            _logger.LogDebug(ex, "Certificate check {Name} failed", check.Name);
            check.LastStatus = MonitorStatus.Down;
            check.LastMessage = ex is OperationCanceledException
                ? _l["certChecks.timeout", check.TimeoutSeconds].Value
                : ex.GetBaseException().Message;
        }
        check.LastCheckedAt = DateTime.UtcNow;
        await _repo.UpdateResult(check);

        if (previous != check.LastStatus)
            _logger.LogInformation("Certificate check {Name}: {Previous} -> {Current} ({Message})",
                check.Name, previous, check.LastStatus, check.LastMessage);
    }

    private static async Task<(X509Certificate2 Cert, SslPolicyErrors Errors)> ReadCertificate(CertCheckDb check, CancellationToken ct)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(check.Host, check.Port, ct);
        var network = tcp.GetStream();
        await StartTlsNegotiator.Negotiate(network, check.Protocol, ct);
        var errors = SslPolicyErrors.None;
        await using var ssl = new SslStream(network, leaveInnerStreamOpen: false);
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = check.ServerName ?? check.Host,
            // accept everything: we only want to read the certificate, the result is reported as ChainValid
            RemoteCertificateValidationCallback = (_, _, _, policyErrors) =>
            {
                errors = policyErrors;
                return true;
            }
        }, ct);
        if (ssl.RemoteCertificate is null)
            throw new InvalidOperationException("No server certificate received");
        // own copy - the stream's certificate is disposed together with the stream
        return (X509CertificateLoader.LoadCertificate(ssl.RemoteCertificate.GetRawCertData()), errors);
    }

    private (string Status, string Message) Evaluate(CertCheckDb check, SslPolicyErrors errors, DateTime nowUtc)
    {
        var days = check.DaysLeft(nowUtc)!.Value;
        var chain = errors == SslPolicyErrors.None ? "" : " · " + _l["certChecks.chainInvalid", errors].Value;
        if (nowUtc < check.NotBefore)
            return (MonitorStatus.Critical, _l["certChecks.notYetValid"].Value + chain);
        if (check.NotAfter < nowUtc)
            return (MonitorStatus.Critical, _l["certChecks.expiredSince", -days].Value + chain);
        var message = _l["certChecks.validFor", days].Value + chain;
        if (days <= _settings.CertCriticalDays)
            return (MonitorStatus.Critical, message);
        if (days <= _settings.CertWarnDays || errors != SslPolicyErrors.None)
            return (MonitorStatus.Warning, message);
        return (MonitorStatus.Ok, message);
    }
}
