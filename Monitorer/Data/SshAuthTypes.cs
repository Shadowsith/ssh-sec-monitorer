namespace Monitorer.Data;

/// <summary>How an SSH credential authenticates.</summary>
public static class SshAuthTypes
{
    public const string Key = "Key";
    public const string Password = "Password";

    public static bool IsValid(string value) => value is Key or Password;
}
