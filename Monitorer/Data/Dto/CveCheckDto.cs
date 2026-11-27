using Monitorer.Data.Db;

namespace Monitorer.Data.Dto;

/// <summary>CVE check for UI / API. Keywords: one per line. Providers: list of CveProviders keys.</summary>
public class CveCheckDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Keywords { get; set; } = "";
    /// <summary>Empty by default: a form without any ticked source must fail validation, not mean "all".</summary>
    public List<string> Providers { get; set; } = [];
    public int IntervalMinutes { get; set; } = 60;
    public bool Enabled { get; set; } = true;
    public bool BaselinePending { get; set; }
    public string LastStatus { get; set; } = MonitorStatus.Unknown;
    public DateTime? LastCheckedAt { get; set; }
    public string? LastMessage { get; set; }
    public int OpenCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<string> KeywordList() =>
        Keywords.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public CveCheckDto() { }

    public CveCheckDto(CveCheckDb row)
    {
        Id = row.Id;
        Name = row.Name;
        Keywords = row.Keywords;
        Providers = row.ProviderList();
        IntervalMinutes = row.IntervalMinutes;
        Enabled = row.Enabled;
        BaselinePending = row.BaselinePending;
        LastStatus = row.LastStatus;
        LastCheckedAt = row.LastCheckedAt;
        LastMessage = row.LastMessage;
        CreatedAt = row.CreatedAt;
    }
}
