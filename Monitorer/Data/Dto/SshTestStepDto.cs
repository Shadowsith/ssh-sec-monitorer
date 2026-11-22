namespace Monitorer.Data.Dto;

/// <summary>One step of an SSH connection test (host reachable, host key, login, SFTP, file).</summary>
public class SshTestStepDto
{
    public string Step { get; set; } = "";
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
}
