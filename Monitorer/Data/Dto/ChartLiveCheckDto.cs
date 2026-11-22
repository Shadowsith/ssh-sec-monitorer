namespace Monitorer.Data.Dto;

/// <summary>Response time of one live check for the overview bar chart.</summary>
public class ChartLiveCheckDto
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public int? ResponseMs { get; set; }
}
