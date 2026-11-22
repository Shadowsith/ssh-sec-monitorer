using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>CRUD for live checks of reachable web services (HTTP(S), WS(S), MQTT(S)).</summary>
public class LiveCheckService
{
    private readonly LiveCheckRepository _repo;
    private readonly SecretService _secrets;

    public LiveCheckService(LiveCheckRepository repo, SecretService secrets)
    {
        _repo = repo;
        _secrets = secrets;
    }

    public async Task<ServiceResult<List<LiveCheckDto>>> GetAll() =>
        ServiceResult<List<LiveCheckDto>>.Ok((await _repo.Select()).Select(r => new LiveCheckDto(r)).ToList());

    public async Task<ServiceResult<LiveCheckDto>> Get(int id)
    {
        var row = await _repo.SelectById(id);
        return row is null
            ? ServiceResult<LiveCheckDto>.NotFound($"Live-Check {id} nicht gefunden.")
            : ServiceResult<LiveCheckDto>.Ok(new LiveCheckDto(row));
    }

    public async Task<ServiceResult<LiveCheckDto>> Create(LiveCheckDto dto)
    {
        var row = new LiveCheckDb();
        var error = Apply(dto, row);
        if (error is not null)
            return ServiceResult<LiveCheckDto>.InvalidValue(error);
        if (await _repo.SelectByName(row.Name) is not null)
            return ServiceResult<LiveCheckDto>.Conflict("Ein Live-Check mit diesem Namen existiert bereits.");

        row.Id = await _repo.Insert(row);
        row.CreatedAt = DateTime.UtcNow;
        return ServiceResult<LiveCheckDto>.Created(new LiveCheckDto(row));
    }

    public async Task<ServiceResult<LiveCheckDto>> Update(int id, LiveCheckDto dto)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<LiveCheckDto>.NotFound($"Live-Check {id} nicht gefunden.");
        var error = Apply(dto, row);
        if (error is not null)
            return ServiceResult<LiveCheckDto>.InvalidValue(error);
        var sameName = await _repo.SelectByName(row.Name);
        if (sameName is not null && sameName.Id != id)
            return ServiceResult<LiveCheckDto>.Conflict("Ein Live-Check mit diesem Namen existiert bereits.");

        await _repo.Update(row);
        return ServiceResult<LiveCheckDto>.Changed(new LiveCheckDto(row));
    }

    public async Task<ServiceResult<LiveCheckDto>> Delete(int id)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<LiveCheckDto>.NotFound($"Live-Check {id} nicht gefunden.");
        await _repo.Delete(id);
        return ServiceResult<LiveCheckDto>.Ok(new LiveCheckDto(row));
    }

    private string? Apply(LiveCheckDto dto, LiveCheckDb row)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 100)
            return "Name fehlt oder ist zu lang (max. 100 Zeichen).";
        if (!LiveCheckTypes.IsValid(dto.CheckType))
            return "Ungültiger Check-Typ.";
        if (!Uri.TryCreate(dto.Target?.Trim(), UriKind.Absolute, out var uri))
            return "Ziel muss eine absolute URL sein.";

        var allowed = dto.CheckType switch
        {
            LiveCheckTypes.Http => new[] { "http", "https" },
            LiveCheckTypes.WebSocket => new[] { "ws", "wss" },
            _ => new[] { "mqtt", "mqtts" }
        };
        if (!allowed.Contains(uri.Scheme))
            return $"Für {dto.CheckType} sind nur {string.Join(" / ", allowed.Select(s => s + "://"))} erlaubt.";
        if (dto.IntervalSeconds is < 5 or > 86400)
            return "Intervall muss zwischen 5 und 86400 Sekunden liegen.";
        if (dto.TimeoutSeconds is < 1 or > 120)
            return "Timeout muss zwischen 1 und 120 Sekunden liegen.";
        if (dto.ExpectedStatusCode is not null and (< 100 or > 599))
            return "Erwarteter Statuscode muss zwischen 100 und 599 liegen.";

        row.Name = dto.Name.Trim();
        row.CheckType = dto.CheckType;
        row.Target = dto.Target!.Trim();
        row.IntervalSeconds = dto.IntervalSeconds;
        row.TimeoutSeconds = dto.TimeoutSeconds;
        row.ExpectedStatusCode = dto.CheckType == LiveCheckTypes.Http ? dto.ExpectedStatusCode : null;
        row.IgnoreTlsErrors = dto.IgnoreTlsErrors;
        row.Enabled = dto.Enabled;
        if (dto.CheckType == LiveCheckTypes.Mqtt)
        {
            row.MqttUserName = string.IsNullOrWhiteSpace(dto.MqttUserName) ? null : dto.MqttUserName.Trim();
            if (!string.IsNullOrEmpty(dto.MqttPassword))
                row.MqttPasswordEnc = _secrets.Protect(dto.MqttPassword);
            if (row.MqttUserName is null)
                row.MqttPasswordEnc = null;
        }
        else
        {
            row.MqttUserName = null;
            row.MqttPasswordEnc = null;
        }
        return null;
    }
}
