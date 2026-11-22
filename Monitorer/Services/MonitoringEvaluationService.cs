using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Data.Monitoring;

namespace Monitorer.Services;

/// <summary>Parses monitoring.json and derives a status (Ok / Warning / Critical) from thresholds.</summary>
public class MonitoringEvaluationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly MonitoringSettings _settings;
    private readonly IStringLocalizer _l;

    public MonitoringEvaluationService(IOptions<MonitoringSettings> settings, IStringLocalizer l)
    {
        _settings = settings.Value;
        _l = l;
    }

    /// <summary>Throws JsonException if the content is not a valid monitoring.json.</summary>
    public MonitoringFileDto Parse(string json)
    {
        var file = JsonSerializer.Deserialize<MonitoringFileDto>(json, JsonOptions)
                   ?? throw new JsonException("monitoring.json is empty.");
        if (file.SchemaVersion != 1)
            throw new JsonException($"Unsupported schema_version {file.SchemaVersion}.");
        return file;
    }

    public EvaluationDto Evaluate(MonitoringFileDto file, DateTime nowUtc)
    {
        var result = new EvaluationDto();

        if (file.GeneratedAt is null)
            result.Raise(MonitorStatus.Warning, _l["finding.noTimestamp"].Value);
        else if ((nowUtc - file.GeneratedAt.Value).TotalSeconds > _settings.StaleAfterSeconds)
            result.Raise(MonitorStatus.Warning,
                _l["finding.stale", file.GeneratedAt.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)].Value);

        var host = file.Host?.Current;
        if (host is not null)
        {
            Check(result, "CPU", host.CpuPercent, _settings.CpuWarnPercent, _settings.CpuCriticalPercent);
            Check(result, "RAM", host.Memory?.Percent, _settings.MemoryWarnPercent, _settings.MemoryCriticalPercent);
            foreach (var disk in host.Disks ?? [])
                Check(result, _l["finding.disk", disk.Mount].Value, disk.Percent, _settings.DiskWarnPercent, _settings.DiskCriticalPercent);
        }

        var docker = file.Docker;
        if (docker is not null)
        {
            if (!docker.Available)
                result.Raise(MonitorStatus.Warning, _l["containers.dockerUnavailable", docker.Error ?? ""].Value);
            foreach (var c in docker.Containers)
            {
                if (c.IsUnhealthy)
                    result.Raise(MonitorStatus.Critical, _l["finding.containerUnhealthy", c.Name].Value);
                else if (c.State is "restarting" or "dead")
                    result.Raise(MonitorStatus.Critical, _l["finding.containerState", c.Name, c.State].Value);
                else if (c.State == "exited" && c.ExitCode is not null and not 0)
                    result.Raise(MonitorStatus.Warning, _l["finding.containerExited", c.Name, c.ExitCode].Value);
            }
        }

        return result;
    }

    private void Check(EvaluationDto result, string name, double? value, double warn, double critical)
    {
        if (value is null)
            return;
        var text = value.Value.ToString("0.0", CultureInfo.InvariantCulture);
        if (value >= critical)
            result.Raise(MonitorStatus.Critical, _l["finding.critical", name, text, critical].Value);
        else if (value >= warn)
            result.Raise(MonitorStatus.Warning, _l["finding.warning", name, text, warn].Value);
    }
}
