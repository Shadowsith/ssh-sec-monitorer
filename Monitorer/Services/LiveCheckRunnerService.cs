using System.Diagnostics;
using System.Net.WebSockets;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Repository;
using MQTTnet;
using MQTTnet.Client;

namespace Monitorer.Services;

/// <summary>Executes live checks: HTTP(S) request, WS(S) handshake, MQTT(S) connect.</summary>
public class LiveCheckRunnerService
{
    public const string HttpClientName = "LiveCheck";
    public const string InsecureHttpClientName = "LiveCheckInsecure";

    private readonly LiveCheckRepository _repo;
    private readonly SecretService _secrets;
    private readonly IHttpClientFactory _httpClients;
    private readonly PollScheduler _scheduler;
    private readonly LiveUpdateService _live;
    private readonly ILogger<LiveCheckRunnerService> _logger;

    public LiveCheckRunnerService(LiveCheckRepository repo, SecretService secrets,
        IHttpClientFactory httpClients, PollScheduler scheduler, LiveUpdateService live, ILogger<LiveCheckRunnerService> logger)
    {
        _live = live;
        _scheduler = scheduler;
        _repo = repo;
        _secrets = secrets;
        _httpClients = httpClients;
        _logger = logger;
    }

    public async Task RunDue(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        // same load handling as the agent polls: spread over the interval, no double runs
        var due = (await _repo.SelectEnabled())
            .Where(c => !_scheduler.IsRunning(PollScheduler.LiveCheckKey(c.Id))
                        && _scheduler.IsDue(PollScheduler.LiveCheckKey(c.Id), c.IntervalSeconds, c.LastCheckedAt, now))
            .OrderBy(c => c.LastCheckedAt ?? DateTime.MinValue)
            .ToList();
        await Parallel.ForEachAsync(due, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
            async (check, token) => await RunGuarded(check, token));
    }

    /// <summary>POST api/live-checks/{id}/run.</summary>
    public async Task<ServiceResult<LiveCheckDto>> RunNow(int id, CancellationToken ct)
    {
        var check = await _repo.SelectById(id);
        if (check is null)
            return ServiceResult<LiveCheckDto>.NotFound($"Live-Check {id} nicht gefunden.");
        if (!await RunGuarded(check, ct))
            return ServiceResult<LiveCheckDto>.Locked("Dieser Live-Check läuft gerade schon.");
        return ServiceResult<LiveCheckDto>.Ok(new LiveCheckDto(check));
    }

    private async Task<bool> RunGuarded(LiveCheckDb check, CancellationToken ct)
    {
        var key = PollScheduler.LiveCheckKey(check.Id);
        if (!_scheduler.TryStart(key))
            return false;
        try
        {
            await RunAndStore(check, ct);
            await _live.Publish(LiveUpdateService.LiveChecks);
            return true;
        }
        finally
        {
            _scheduler.Finish(key);
        }
    }

    private async Task RunAndStore(LiveCheckDb check, CancellationToken ct)
    {
        var previous = check.LastStatus;
        var result = await Execute(check, ct);
        check.LastStatus = result.Status;
        check.LastResponseMs = result.ResponseMs;
        check.LastMessage = result.Message;
        check.LastCheckedAt = DateTime.UtcNow;
        await _repo.UpdateResult(check);

        if (previous != result.Status)
            _logger.LogInformation("Live check {Name}: {Previous} -> {Current} ({Message})",
                check.Name, previous, result.Status, result.Message);
    }

    private async Task<LiveCheckResultDto> Execute(LiveCheckDb check, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(check.TimeoutSeconds));
        var watch = Stopwatch.StartNew();
        try
        {
            var (status, message) = check.CheckType switch
            {
                LiveCheckTypes.Http => await CheckHttp(check, timeout.Token),
                LiveCheckTypes.WebSocket => await CheckWebSocket(check, timeout.Token),
                LiveCheckTypes.Mqtt => await CheckMqtt(check, timeout.Token),
                _ => throw new InvalidOperationException($"Unknown check type {check.CheckType}")
            };
            return new LiveCheckResultDto { Status = status, ResponseMs = (int)watch.ElapsedMilliseconds, Message = message };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new LiveCheckResultDto { Status = MonitorStatus.Down, Message = $"Timeout nach {check.TimeoutSeconds} s" };
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Live check {Name} failed", check.Name);
            return new LiveCheckResultDto { Status = MonitorStatus.Down, Message = ex.GetBaseException().Message };
        }
    }

    private async Task<(string Status, string Message)> CheckHttp(LiveCheckDb check, CancellationToken ct)
    {
        var client = _httpClients.CreateClient(check.IgnoreTlsErrors ? InsecureHttpClientName : HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, check.Target);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var code = (int)response.StatusCode;
        var ok = check.ExpectedStatusCode is null ? code < 400 : code == check.ExpectedStatusCode;
        if (!ok)
            throw new InvalidOperationException($"HTTP {code} {response.ReasonPhrase}"
                + (check.ExpectedStatusCode is null ? "" : $" (erwartet {check.ExpectedStatusCode})"));
        return (MonitorStatus.Up, $"HTTP {code}");
    }

    private static async Task<(string Status, string Message)> CheckWebSocket(LiveCheckDb check, CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        socket.Options.CollectHttpResponseDetails = true;
        if (check.IgnoreTlsErrors)
            socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        try
        {
            await socket.ConnectAsync(new Uri(check.Target), ct);
        }
        catch (WebSocketException) when ((int)socket.HttpStatusCode is >= 400 and < 500)
        {
            // the service answered the handshake (e.g. 401 before auth) -> online, but rejects the check
            var code = (int)socket.HttpStatusCode;
            return (MonitorStatus.Degraded, $"HTTP {code} statt 101 beim WebSocket-Handshake");
        }
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "monitor check", ct);
        return (MonitorStatus.Up, "WebSocket-Handshake erfolgreich");
    }

    private async Task<(string Status, string Message)> CheckMqtt(LiveCheckDb check, CancellationToken ct)
    {
        var uri = new Uri(check.Target);
        var useTls = uri.Scheme == "mqtts";
        var port = uri.IsDefaultPort || uri.Port <= 0 ? (useTls ? 8883 : 1883) : uri.Port;

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(uri.Host, port)
            .WithClientId($"ssm-check-{Guid.NewGuid():N}")
            .WithTimeout(TimeSpan.FromSeconds(check.TimeoutSeconds))
            .WithCleanSession();
        if (!string.IsNullOrEmpty(check.MqttUserName))
            builder = builder.WithCredentials(check.MqttUserName, _secrets.Unprotect(check.MqttPasswordEnc));
        if (useTls)
            builder = builder.WithTlsOptions(o =>
            {
                o.UseTls();
                if (check.IgnoreTlsErrors)
                    o.WithCertificateValidationHandler(_ => true);
            });

        using var client = new MqttFactory().CreateMqttClient();
        var result = await client.ConnectAsync(builder.Build(), ct);
        await client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), CancellationToken.None);
        return (MonitorStatus.Up, $"MQTT verbunden ({result.ResultCode})");
    }
}
