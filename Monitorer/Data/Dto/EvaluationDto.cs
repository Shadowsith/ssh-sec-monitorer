namespace Monitorer.Data.Dto;

/// <summary>Status computed from a monitoring.json plus the reasons.</summary>
public class EvaluationDto
{
    public string Status { get; set; } = MonitorStatus.Ok;
    public List<string> Findings { get; set; } = [];

    public void Raise(string status, string finding)
    {
        Findings.Add(finding);
        if (MonitorStatus.Severity(status) > MonitorStatus.Severity(Status))
            Status = status;
    }
}
