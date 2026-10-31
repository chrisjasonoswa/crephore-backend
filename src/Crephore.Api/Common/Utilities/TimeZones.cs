using System.Diagnostics.CodeAnalysis;

namespace Crephore.Api.Common.Utilities;

public static class TimeZones
{
    // IANA ids only (e.g. "Asia/Manila"), which browsers can format with directly.
    public static bool TryFind(string? id, [NotNullWhen(true)] out TimeZoneInfo? timeZone)
    {
        timeZone = null;
        if (string.IsNullOrWhiteSpace(id) || !TimeZoneInfo.TryFindSystemTimeZoneById(id, out var found) || !found.HasIanaId)
            return false;

        timeZone = found;
        return true;
    }

    // A wall-clock time in `timeZone` → the UTC instant.
    // Null when that time does not exist there (skipped when the clocks go forward).
    public static DateTimeOffset? ToUtc(DateTime wallClock, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
            return null;

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }
}
