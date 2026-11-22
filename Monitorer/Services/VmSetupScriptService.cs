namespace Monitorer.Services;

/// <summary>
/// Provides the shell scripts that create the read-only SFTP user on an agent VM (Assets/Scripts),
/// one per supported system. The scripts share the same structure; only the OS check, the package hint
/// and the name of the SSH service differ - keep them in sync when changing one.
/// </summary>
public class VmSetupScriptService
{
    /// <summary>One script variant: key, label for the select, file name.</summary>
    public record Variant(string Key, string Label, string FileName);

    public static readonly IReadOnlyList<Variant> Variants =
    [
        new("ubuntu", "Ubuntu ≥ 22.04", "setup-sftp-user-ubuntu.sh"),
        new("arch", "Arch Linux / Manjaro", "setup-sftp-user-arch.sh"),
        new("raspios", "Raspberry Pi OS / Raspbian ≥ 11", "setup-sftp-user-raspios.sh")
    ];

    private static readonly string ScriptDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts");

    /// <summary>All variants with their script text (placeholders like &lt;PFAD_ZUM_AGENT_DATENORDNER&gt;, LF line endings).</summary>
    public async Task<List<(Variant Variant, string Script)>> GetAll()
    {
        var result = new List<(Variant, string)>();
        foreach (var variant in Variants)
        {
            var text = await File.ReadAllTextAsync(Path.Combine(ScriptDirectory, variant.FileName));
            result.Add((variant, text.ReplaceLineEndings("\n")));
        }
        return result;
    }
}
