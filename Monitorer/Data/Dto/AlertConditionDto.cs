namespace Monitorer.Data.Dto;

/// <summary>A currently fulfilled alert condition (one affected VM / service / container).</summary>
public class AlertConditionDto
{
    public string SubjectKey { get; set; } = "";
    public string SubjectName { get; set; } = "";
    public string Message { get; set; } = "";
}
