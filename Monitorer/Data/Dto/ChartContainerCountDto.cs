namespace Monitorer.Data.Dto;

/// <summary>Container counts of one VM for the overview stacked bar chart.</summary>
public class ChartContainerCountDto
{
    public string Name { get; set; } = "";
    public int Running { get; set; }
    public int Problem { get; set; }
    public int Stopped { get; set; }
    public int Missing { get; set; }
}
