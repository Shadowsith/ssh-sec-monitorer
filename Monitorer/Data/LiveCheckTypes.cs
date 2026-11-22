namespace Monitorer.Data;

/// <summary>Supported live check protocols.</summary>
public static class LiveCheckTypes
{
    public const string Http = "Http";          // http:// and https://
    public const string WebSocket = "WebSocket"; // ws:// and wss://
    public const string Mqtt = "Mqtt";          // mqtt:// and mqtts://

    public static readonly string[] All = [Http, WebSocket, Mqtt];

    public static bool IsValid(string value) => All.Contains(value);
}
