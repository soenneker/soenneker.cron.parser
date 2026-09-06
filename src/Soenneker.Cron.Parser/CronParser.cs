using System;

namespace Soenneker.Cron.Parser;

/// <summary>Parses Cronos-compatible expressions into immutable reusable schedules.</summary>
public static class CronParser
{
    /// <summary>Parses an expression using five fields, or six with leading seconds when enabled.</summary>
    /// <param name="expression">A Cronos-compatible expression, including names, ranges, L, W, #, ?, and macros.</param>
    /// <param name="timeZoneId">The system time zone identifier; defaults to UTC.</param>
    /// <param name="includeSeconds">Whether the expression contains leading seconds.</param>
    /// <param name="jitterSeed">An optional deterministic seed for H expressions and jittered macros.</param>
    /// <returns>A reusable schedule. Numeric UTC expressions require no heap allocation.</returns>
    public static CronSchedule Parse(string expression, string timeZoneId = "UTC", bool includeSeconds = false, int? jitterSeed = null)
        => new(expression, timeZoneId, includeSeconds, jitterSeed);

    /// <summary>Parses a schedule using an already resolved time zone.</summary>
    /// <param name="expression">The cron expression.</param>
    /// <param name="timeZone">The time zone in which to evaluate occurrences.</param>
    /// <param name="includeSeconds">Whether leading seconds are enabled.</param>
    /// <param name="jitterSeed">An optional deterministic jitter seed.</param>
    public static CronSchedule Parse(string expression, TimeZoneInfo timeZone, bool includeSeconds = false, int? jitterSeed = null)
        => new(expression, timeZone, includeSeconds, jitterSeed);

    /// <summary>Parses a character span without copying it into a string. The input is not retained.</summary>
    /// <param name="expression">The cron expression, optionally sliced from a larger buffer.</param>
    /// <param name="timeZone">A resolved time zone, or null for UTC.</param>
    /// <param name="includeSeconds">Whether leading seconds are enabled.</param>
    /// <param name="jitterSeed">An optional deterministic jitter seed.</param>
    public static CronSchedule Parse(ReadOnlySpan<char> expression, TimeZoneInfo? timeZone = null, bool includeSeconds = false, int? jitterSeed = null)
        => new(expression, timeZone ?? TimeZoneInfo.Utc, includeSeconds, jitterSeed);

    /// <summary>Attempts to parse an expression. Invalid syntax and null input return false; time-zone lookup errors propagate.</summary>
    /// <param name="expression">The expression to validate.</param>
    /// <param name="schedule">The parsed schedule, or its uninitialized default value on failure.</param>
    /// <param name="timeZoneId">The time zone identifier; defaults to UTC.</param>
    /// <param name="includeSeconds">Whether leading seconds are enabled.</param>
    /// <param name="jitterSeed">An optional deterministic jitter seed.</param>
    /// <returns>Whether parsing succeeded.</returns>
    public static bool TryParse(string? expression, out CronSchedule schedule, string timeZoneId = "UTC", bool includeSeconds = false, int? jitterSeed = null)
    {
        schedule = default;
        if (expression is null) return false;
        try { schedule = new CronSchedule(expression, timeZoneId, includeSeconds, jitterSeed); return true; }
        catch (FormatException) { return false; }
    }
}
