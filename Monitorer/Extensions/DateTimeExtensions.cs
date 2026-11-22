using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Monitorer.Extensions;

/// <summary>
/// UTC in the database and API, Berlin time for humans and schedules.
/// Formats come from the format.* translation keys, numbers follow the current culture.
/// </summary>
public static class DateTimeExtensions
{
    public static readonly TimeZoneInfo BerlinTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public static DateTime BerlinNow => DateTime.UtcNow.ToBerlin();

    public static DateTime ToBerlin(this DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), BerlinTimeZone);

    public static DateTime ToUtcFromBerlin(this DateTime berlin) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(berlin, DateTimeKind.Unspecified), BerlinTimeZone);

    /// <summary>Berlin time with a format.* key, e.g. "format.dateTime".</summary>
    public static string ToBerlinString(this DateTime utc, IStringLocalizer l, string formatKey = "format.dateTime") =>
        utc.ToBerlin().ToString(l[formatKey].Value, CultureInfo.CurrentCulture);

    public static string ToBerlinString(this DateTime? utc, IStringLocalizer l, string formatKey = "format.dateTime", string empty = "–") =>
        utc is null ? empty : utc.Value.ToBerlinString(l, formatKey);

    /// <summary>"3 min ago" style text for the UI.</summary>
    public static string ToAgeString(this DateTime? utc, IStringLocalizer l)
    {
        if (utc is null)
            return l["age.never"].Value;
        var age = DateTime.UtcNow - utc.Value;
        if (age.TotalSeconds < 60) return l["age.seconds", Math.Max(0, (int)age.TotalSeconds)].Value;
        if (age.TotalMinutes < 60) return l["age.minutes", (int)age.TotalMinutes].Value;
        if (age.TotalHours < 48) return l["age.hours", (int)age.TotalHours].Value;
        return l["age.days", (int)age.TotalDays].Value;
    }

    /// <summary>"for 3 days" style text for running durations.</summary>
    public static string ToSinceString(this DateTime? utc, IStringLocalizer l)
    {
        if (utc is null)
            return "–";
        var age = DateTime.UtcNow - utc.Value;
        if (age.TotalMinutes < 1) return l["since.seconds"].Value;
        if (age.TotalMinutes < 60) return l["since.minutes", (int)age.TotalMinutes].Value;
        if (age.TotalHours < 48) return l["since.hours", (int)age.TotalHours].Value;
        return l["since.days", (int)age.TotalDays].Value;
    }
}
