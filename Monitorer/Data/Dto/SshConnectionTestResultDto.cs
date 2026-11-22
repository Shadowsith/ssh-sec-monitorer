namespace Monitorer.Data.Dto;

/// <summary>Result of an SSH connection test: one entry per step.</summary>
public class SshConnectionTestResultDto
{
    public bool Success { get; set; }
    public string? HostKeyFingerprint { get; set; }
    public List<SshTestStepDto> Steps { get; set; } = [];

    public void Add(string step, bool ok, string message) =>
        Steps.Add(new SshTestStepDto { Step = step, Ok = ok, Message = message });
}
