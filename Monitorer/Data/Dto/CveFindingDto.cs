using Monitorer.Data.Db;

namespace Monitorer.Data.Dto;

/// <summary>A found CVE plus the name of the check that found it.</summary>
public class CveFindingDto : CveFindingDb
{
    public string CheckName { get; set; } = "";

    /// <summary>Link for the CVE: the source's page, otherwise OpenCVE.</summary>
    public string Link => Url ?? $"https://app.opencve.io/cve/{CveId}";
}
