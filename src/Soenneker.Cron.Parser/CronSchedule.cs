using System;
using System.Numerics;


namespace Soenneker.Cron.Parser;

/// <summary>An immutable, reusable cron schedule with Cronos-compatible syntax and time-zone behavior.</summary>
/// <remarks>Schedules use inline bit masks and independent span-based parsing.
/// The default value is uninitialized and cannot calculate occurrences.</remarks>
public readonly struct CronSchedule
{
    private const ulong AllMinutes = (1UL << 60) - 1;
    private const uint AllHours = (1u << 24) - 1;
    private const uint AllDays = uint.MaxValue - 1; // Days 1 through 31; bit zero is unused.
    private const ushort AllMonths = (1 << 13) - 2; // Months 1 through 12.
    private const byte AllWeekdays = (1 << 7) - 1;

    private readonly ulong _seconds, _minutes;
    private readonly uint _hours, _days;
    private readonly ushort _months;
    private readonly byte _weekdays;
    private readonly ScheduleFlags _flags;
    private readonly byte _lastOffset, _nth;
    private readonly TimeZoneInfo? _zone;

    /// <summary>Parses a five-field expression, or six fields when seconds are enabled.</summary>
    /// <param name="expression">A Cronos-compatible expression.</param>
    /// <param name="timeZoneId">The system time zone identifier; defaults to UTC.</param>
    /// <param name="includeSeconds">Whether the expression contains leading seconds.</param>
    /// <param name="jitterSeed">The deterministic seed for H expressions and jittered macros.</param>
    /// <exception cref="FormatException">The expression is invalid.</exception>
    /// <exception cref="TimeZoneNotFoundException">The requested time zone is unavailable.</exception>
    public CronSchedule(string expression, string timeZoneId = "UTC", bool includeSeconds = false, int? jitterSeed = null)
        : this(expression, ResolveZone(timeZoneId), includeSeconds, jitterSeed)
    {
    }

    /// <summary>Parses an expression using an already resolved time zone, avoiding repeated time-zone lookup.</summary>
    /// <param name="expression">The cron expression.</param>
    /// <param name="timeZone">The time zone in which the schedule is evaluated.</param>
    /// <param name="includeSeconds">Whether leading seconds are enabled.</param>
    /// <param name="jitterSeed">An optional deterministic jitter seed.</param>
    public CronSchedule(string expression, TimeZoneInfo timeZone, bool includeSeconds = false, int? jitterSeed = null)
        : this((expression ?? throw new ArgumentNullException(nameof(expression))).AsSpan(), timeZone, includeSeconds, jitterSeed)
    {
    }

    internal CronSchedule(ReadOnlySpan<char> expression, TimeZoneInfo timeZone, bool includeSeconds, int? jitterSeed)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        this = default;
        _zone = timeZone;
        Span<ulong> fields = stackalloc ulong[6];
        if (jitterSeed.HasValue || !FastParser.TryParse(expression, includeSeconds, fields, out _flags))
            ExpressionParser.Parse(expression, includeSeconds, jitterSeed, fields, out _flags, out _lastOffset, out _nth);
        _seconds = fields[0];
        _minutes = fields[1];
        _hours = (uint)fields[2];
        _days = (uint)fields[3];
        _months = (ushort)fields[4];
        _weekdays = (byte)((fields[5] | ((fields[5] & 128) >> 7)) & 127);
    }

    private static TimeZoneInfo ResolveZone(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return id == "UTC" ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(id);
    }

    /// <summary>Returns the next matching UTC instant, or null when no occurrence exists.</summary>
    /// <param name="after">The starting instant, in any offset.</param>
    /// <param name="inclusive">Whether an exact matching starting instant is eligible.</param>
    public DateTimeOffset? Next(DateTimeOffset after, bool inclusive = false)
    {
        DateTime? next = GetNextOccurrence(after.UtcDateTime, inclusive);
        return next.HasValue ? new DateTimeOffset(next.Value) : null;
    }

    /// <summary>Returns the next UTC occurrence. The input must have UTC kind.</summary>
    /// <param name="fromUtc">The UTC starting instant.</param>
    /// <param name="inclusive">Whether an exact matching starting instant is eligible.</param>
    /// <exception cref="ArgumentException">The input does not have UTC kind.</exception>
    /// <exception cref="InvalidOperationException">The schedule is the uninitialized default value.</exception>
    public DateTime? GetNextOccurrence(DateTime fromUtc, bool inclusive = false)
    {
        if (fromUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The starting time must be UTC.", nameof(fromUtc));
        if (_zone is null) throw new InvalidOperationException("The schedule has not been initialized.");
        return _zone == TimeZoneInfo.Utc ? FindLocal(fromUtc, inclusive) : FindZoned(fromUtc, inclusive);
    }

    private DateTime? FindLocal(DateTime fromUtc, bool inclusive)
    {
        long ticks = fromUtc.Ticks;
        long remainder = ticks % TimeSpan.TicksPerSecond;
        if (!inclusive || remainder != 0) ticks += TimeSpan.TicksPerSecond - remainder;
        if (ticks > DateTime.MaxValue.Ticks) return null;

        bool everyDate = _months == AllMonths && _days == AllDays && _weekdays == AllWeekdays && (_flags & ~ScheduleFlags.Interval) == 0;
        if (everyDate && _hours == AllHours && _minutes == AllMinutes)
        {
            // Only the second field restricts this schedule. Search this minute, then the next;
            // there is no reason to calculate a calendar date or split hours and minutes.
            int second = (int)(ticks / TimeSpan.TicksPerSecond % 60);
            int nextSecond = AtOrAfter(_seconds, second);
            int advance = nextSecond < 60 ? nextSecond - second : 60 - second + BitOperations.TrailingZeroCount(_seconds);
            long result = ticks + advance * TimeSpan.TicksPerSecond;
            return result <= DateTime.MaxValue.Ticks ? new DateTime(result, DateTimeKind.Utc) : null;
        }

        long dayTicks = ticks - ticks % TimeSpan.TicksPerDay;
        int time = (int)((ticks - dayTicks) / TimeSpan.TicksPerSecond);
        if (everyDate)
        {
            int nextTime = NextTime(time);
            if (nextTime < 0) { dayTicks += TimeSpan.TicksPerDay; nextTime = FirstTime(); }
            long result = dayTicks + nextTime * TimeSpan.TicksPerSecond;
            return result <= DateTime.MaxValue.Ticks ? new DateTime(result, DateTimeKind.Utc) : null;
        }
        var current = new DateTime(ticks, DateTimeKind.Unspecified);
        current.Deconstruct(out int year, out int month, out int day);
        int originalYear = year, originalMonth = month, originalDay = day;
        int lastYear = Math.Min(9999, year + 400);
        while (year <= lastYear)
        {
            int nextMonth = AtOrAfter(_months, month);
            if (nextMonth == 64) { year++; month = 1; day = 1; time = 0; continue; }
            if (nextMonth != month) { month = nextMonth; day = 1; time = 0; }
            uint days = DaysInMonth(year, month);
            int nextDay = AtOrAfter(days, day);
            if (nextDay < 32)
            {
                int nextTime = nextDay == day ? NextTime(time) : FirstTime();
                if (nextTime >= 0)
                {
                    long dateTicks = year == originalYear && month == originalMonth && nextDay == originalDay
                        ? dayTicks : new DateTime(year, month, nextDay).Ticks;
                    return new DateTime(dateTicks + nextTime * TimeSpan.TicksPerSecond, DateTimeKind.Utc);
                }
                day = nextDay + 1;
                time = 0;
                continue;
            }
            month++;
            day = 1;
            time = 0;
        }
        return null;
    }

    private int FirstTime() => BitOperations.TrailingZeroCount(_hours) * 3600 + BitOperations.TrailingZeroCount(_minutes) * 60 + BitOperations.TrailingZeroCount(_seconds);

    private int NextTime(int time)
    {
        int fromHour = time / 3600, fromMinute = time / 60 % 60, fromSecond = time % 60;
        int hour = AtOrAfter(_hours, fromHour);
        while (hour < 24)
        {
            int minute = AtOrAfter(_minutes, hour == fromHour ? fromMinute : 0);
            while (minute < 60)
            {
                int second = AtOrAfter(_seconds, hour == fromHour && minute == fromMinute ? fromSecond : 0);
                if (second < 60) return hour * 3600 + minute * 60 + second;
                minute = AtOrAfter(_minutes, minute + 1);
            }
            hour = AtOrAfter(_hours, hour + 1);
        }
        return -1;
    }
    /// <summary>Writes successive UTC occurrences into caller-owned memory and returns the count written.</summary>
    /// <param name="after">The starting instant.</param>
    /// <param name="destination">Storage for the results.</param>
    /// <param name="inclusive">Whether an exact matching starting instant is eligible for the first result.</param>
    public int FillOccurrences(DateTimeOffset after, Span<DateTimeOffset> destination, bool inclusive = false)
    {
        int count = 0;
        while (count < destination.Length && Next(after, inclusive) is { } next)
        {
            destination[count++] = next;
            after = next;
            inclusive = false;
        }
        return count;
    }

    private uint DaysInMonth(int year, int month)
    {
        int last = DateTime.DaysInMonth(year, month);
        uint days = _days & (uint)((1UL << (last + 1)) - 2);
        if ((_flags & (ScheduleFlags.LastDay | ScheduleFlags.NearestWeekday)) != 0)
        {
            int target = (_flags & ScheduleFlags.LastDay) != 0 ? last - _lastOffset : BitOperations.TrailingZeroCount(_days);
            if (target < 1 || target > last) return 0;
            if ((_flags & ScheduleFlags.NearestWeekday) != 0)
            {
                int weekday = (int)new DateTime(year, month, target).DayOfWeek;
                if (weekday == 6) target += target == 1 ? 2 : -1;
                else if (weekday == 0) target += target == last ? -2 : 1;
            }
            days = 1u << target;
        }
        if ((_flags & ScheduleFlags.LastWeekday) != 0) days &= uint.MaxValue << (last - 6);
        if ((_flags & ScheduleFlags.NthWeekday) != 0) days &= (uint)(127UL << (1 + (_nth - 1) * 7));
        if (_weekdays != AllWeekdays)
        {
            int first = (int)new DateTime(year, month, 1).DayOfWeek;
            uint week = (uint)((_weekdays >> first) | (_weekdays << (7 - first))) & 127;
            uint repeated = week | (week << 7) | (week << 14) | (week << 21) | (week << 28);
            days &= repeated << 1;
        }
        return days;
    }
    private DateTime? FindZoned(DateTime fromUtc, bool inclusive)
    {
        TimeZoneInfo zone = _zone!;
        long fractional = fromUtc.Ticks % TimeSpan.TicksPerSecond;
        if (fractional != 0) { fromUtc = fromUtc.AddTicks(-fractional); inclusive = false; }
        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, zone);
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        // ConvertTimeFromUtc clamps a local date before year 1 to MinValue. That boundary is
        // still in the future relative to the actual starting instant, so include its first match.
        if (local.Ticks == 0 && fromUtc.Ticks + zone.GetUtcOffset(fromUtc).Ticks < 0) inclusive = true;
        if (zone.IsAmbiguousTime(local))
        {
            DateTime start = ZoneBoundary(zone, local, true, false);
            DateTime end = ZoneBoundary(zone, local, true, true);
            TimeSpan early = zone.GetUtcOffset(start.AddTicks(-1));
            TimeSpan late = zone.GetUtcOffset(end);
            bool firstPass = zone.GetUtcOffset(fromUtc) == early;
            if (firstPass)
            {
                DateTime? candidate = FindLocal(local, inclusive);
                if (candidate.HasValue && candidate.Value.Ticks < end.Ticks) return ToUtc(candidate.Value, early);
                local = start;
                inclusive = true;
            }
            if ((_flags & ScheduleFlags.Interval) != 0)
            {
                DateTime? candidate = FindLocal(local, inclusive);
                if (candidate.HasValue && candidate.Value.Ticks < end.Ticks) return ToUtc(candidate.Value, late);
            }
            local = end;
            inclusive = true;
        }
        DateTime? match = FindLocal(local, inclusive);
        if (match is null) return null;
        DateTime wall = DateTime.SpecifyKind(match.Value, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(wall)) wall = ZoneBoundary(zone, wall, false, true);
        TimeSpan offset;
        if (zone.IsAmbiguousTime(wall))
        {
            DateTime start = ZoneBoundary(zone, wall, true, false);
            offset = zone.GetUtcOffset(start.AddTicks(-1));
        }
        else offset = zone.GetUtcOffset(wall);
        return ToUtc(wall, offset);
    }

    private static DateTime? ToUtc(DateTime wall, TimeSpan offset)
    {
        long ticks = wall.Ticks - offset.Ticks;
        return ticks < 0 || ticks > DateTime.MaxValue.Ticks ? null : new DateTime(ticks, DateTimeKind.Utc);
    }

    // Locate the edge of a gap or overlap without allocating ambiguous-offset arrays.
    // Exponential bracketing handles non-hour transitions; binary search preserves second precision.
    private static DateTime ZoneBoundary(TimeZoneInfo zone, DateTime inside, bool overlap, bool forward)
    {
        long origin = inside.Ticks / TimeSpan.TicksPerSecond;
        long max = DateTime.MaxValue.Ticks / TimeSpan.TicksPerSecond;
        long inner = origin, outer;
        long step = 1;
        while (true)
        {
            outer = Math.Clamp(origin + (forward ? step : -step), 0, max);
            var probe = new DateTime(outer * TimeSpan.TicksPerSecond, DateTimeKind.Unspecified);
            bool contained = overlap ? zone.IsAmbiguousTime(probe) : zone.IsInvalidTime(probe);
            if (!contained) break;
            if (outer == 0 || outer == max) return probe;
            inner = outer;
            step *= 2;
        }
        long low = Math.Min(inner, outer), high = Math.Max(inner, outer);
        while (high - low > 1)
        {
            long middle = low + (high - low) / 2;
            var probe = new DateTime(middle * TimeSpan.TicksPerSecond, DateTimeKind.Unspecified);
            bool contained = overlap ? zone.IsAmbiguousTime(probe) : zone.IsInvalidTime(probe);
            if (contained == forward) low = middle;
            else high = middle;
        }
        return new DateTime(high * TimeSpan.TicksPerSecond, DateTimeKind.Unspecified);
    }
    private static int AtOrAfter(ulong bits, int value) => BitOperations.TrailingZeroCount(bits & (ulong.MaxValue << value));
}
