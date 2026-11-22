using System.Globalization;

namespace Monitorer.Extensions;

/// <summary>UTC in the database and API, Berlin time for humans and schedules.</summary>
public static class DateTimeExtensions
{
    public static readonly TimeZoneInfo BerlinTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    public static readonly CultureInfo BerlinCulture = CultureInfo.GetCultureInfo("de-DE");

    public static DateTime BerlinNow => DateTime.UtcNow.ToBerlin();

    public static DateTime ToBerlin(this DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), BerlinTimeZone);

    public static DateTime ToUtcFromBerlin(this DateTime berlin) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(berlin, DateTimeKind.Unspecified), BerlinTimeZone);

    public static string ToBerlinString(this DateTime utc, string format = "dd.MM.yyyy HH:mm") =>
        utc.ToBerlin().ToString(format, BerlinCulture);

    public static string ToBerlinString(this DateTime? utc, string format = "dd.MM.yyyy HH:mm", string empty = "–") =>
        utc is null ? empty : utc.Value.ToBerlinString(format);

    /// <summary>"vor 3 min" style text for the UI.</summary>
    public static string ToAgeString(this DateTime? utc)
    {
        if (utc is null)
            return "nie";
        var age = DateTime.UtcNow - utc.Value;
        if (age.TotalSeconds < 60) return $"vor {Math.Max(0, (int)age.TotalSeconds)} s";
        if (age.TotalMinutes < 60) return $"vor {(int)age.TotalMinutes} min";
        if (age.TotalHours < 48) return $"vor {(int)age.TotalHours} h";
        return $"vor {(int)age.TotalDays} Tagen";
    }

    /// <summary>"seit 3 Tagen" style text for running durations.</summary>
    public static string ToSinceString(this DateTime? utc)
    {
        if (utc is null)
            return "–";
        var age = DateTime.UtcNow - utc.Value;
        if (age.TotalMinutes < 1) return "seit wenigen Sekunden";
        if (age.TotalMinutes < 60) return $"seit {(int)age.TotalMinutes} min";
        if (age.TotalHours < 48) return $"seit {(int)age.TotalHours} h";
        return $"seit {(int)age.TotalDays} Tagen";
    }
}
