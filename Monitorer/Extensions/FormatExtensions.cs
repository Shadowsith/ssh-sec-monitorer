namespace Monitorer.Extensions;

/// <summary>Number formatting for the views (current culture: request language, default language in the background).</summary>
public static class FormatExtensions
{
    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string ToByteString(this long? bytes) => bytes is null ? "–" : ((double)bytes.Value).ToByteString();

    public static string ToByteString(this double bytes)
    {
        var unit = 0;
        while (Math.Abs(bytes) >= 1024 && unit < ByteUnits.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }
        return bytes.ToString(unit == 0 ? "0" : "0.0", System.Globalization.CultureInfo.CurrentCulture) + " " + ByteUnits[unit];
    }

    public static string ToRateString(this double? bytesPerSecond) =>
        bytesPerSecond is null ? "–" : bytesPerSecond.Value.ToByteString() + "/s";

    public static string ToPercentString(this double? value) =>
        value is null ? "–" : value.Value.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture) + " %";

    public static string ToPercentString(this decimal? value) =>
        value is null ? "–" : value.Value.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture) + " %";

    /// <summary>Invariant number 0–100 for CSS widths / aria values (never "40,5").</summary>
    public static string ToCssNumber(this double? percent) =>
        Math.Clamp(percent ?? 0, 0, 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    public static string ToDurationString(this double? seconds)
    {
        if (seconds is null)
            return "–";
        var t = TimeSpan.FromSeconds(seconds.Value);
        return t.TotalDays >= 1 ? $"{(int)t.TotalDays} d {t.Hours} h" : $"{t.Hours} h {t.Minutes} min";
    }
}
