using System;

namespace Soenneker.Cron.Parser;

[Flags]
internal enum ScheduleFlags : byte { None = 0, Interval = 1, LastDay = 2, NearestWeekday = 4, LastWeekday = 8, NthWeekday = 16 }

internal static class ExpressionParser
{
    internal static void Parse(ReadOnlySpan<char> text, bool seconds, int? seed, Span<ulong> fields,
        out ScheduleFlags flags, out byte lastOffset, out byte nth)
    {
        flags = 0;
        lastOffset = nth = 0;
        text = text.Trim(" \t");
        if (text.StartsWith("@"))
        {
            bool jitter = seed.HasValue;
            seconds = jitter;
            if (text.Equals("@yearly", StringComparison.OrdinalIgnoreCase) || text.Equals("@annually", StringComparison.OrdinalIgnoreCase)) text = jitter ? "H H H H H *" : "0 0 1 1 *";
            else if (text.Equals("@monthly", StringComparison.OrdinalIgnoreCase)) text = jitter ? "H H H H * *" : "0 0 1 * *";
            else if (text.Equals("@weekly", StringComparison.OrdinalIgnoreCase)) text = jitter ? "H H H * * H" : "0 0 * * 0";
            else if (text.Equals("@daily", StringComparison.OrdinalIgnoreCase) || text.Equals("@midnight", StringComparison.OrdinalIgnoreCase)) text = jitter ? "H H H * * *" : "0 0 * * *";
            else if (text.Equals("@hourly", StringComparison.OrdinalIgnoreCase)) text = jitter ? "H H * * * *" : "0 * * * *";
            else if (text.Equals("@every_minute", StringComparison.OrdinalIgnoreCase)) text = jitter ? "H * * * * *" : "* * * * *";
            else if (text.Equals("@every_second", StringComparison.OrdinalIgnoreCase)) { text = "* * * * * *"; seconds = true; }
            else throw Invalid();
        }
        Random? random = seed.HasValue ? new Random(seed.Value) : null;
        int position = 0;
        fields[0] = 1;
        for (int field = seconds ? 0 : 1; field < 6; field++)
        {
            while (position < text.Length && text[position] is ' ' or '\t') position++;
            int start = position;
            while (position < text.Length && text[position] is not (' ' or '\t')) position++;
            var part = text[start..position];
            if (part.Length == 1)
            {
                char token = part[0];
                if (token is '*' or '?')
                {
                    fields[field] = field switch { 0 or 1 => (1UL << 60) - 1, 2 => (1UL << 24) - 1, 3 => 0xFFFFFFFE, 4 => 8190, _ => 255 };
                    if (field < 3) flags |= ScheduleFlags.Interval;
                    continue;
                }
                int digit = token - '0';
                if ((uint)digit < 10 && (field is not (3 or 4) || digit != 0) && (field != 5 || digit <= 7))
                {
                    fields[field] = 1UL << digit;
                    continue;
                }
            }
            fields[field] = Field(part, field, random, ref flags, ref lastOffset, ref nth);
        }
        while (position < text.Length && text[position] is ' ' or '\t') position++;
        if (position != text.Length) throw Invalid();
    }

    private static ulong Field(ReadOnlySpan<char> text, int field, Random? random, ref ScheduleFlags flags, ref byte lastOffset, ref byte nth)
    {
        if (text.IsEmpty) throw Invalid();
        int min = field is 3 or 4 ? 1 : 0;
        int max = field switch { 0 or 1 => 59, 2 => 23, 3 => 31, 4 => 12, _ => 7 };
        int p = 0;
        if (field == 3 && (text[0] is 'L' or 'l'))
        {
            p++;
            flags |= ScheduleFlags.LastDay;
            if (p < text.Length && text[p] == '-') { p++; lastOffset = (byte)Number(text, ref p, 0, 30); }
            if (p < text.Length && (text[p] is 'W' or 'w')) { p++; flags |= ScheduleFlags.NearestWeekday; }
            if (p != text.Length) throw Invalid();
            return 0xFFFFFFFE;
        }
        if (text[0] == 'H')
        {
            if (random is null) throw new FormatException("H requires a jitter seed.");
            p++;
            int limit = field == 3 ? 28 : max;
            int step = 0;
            if (p < text.Length && text[p] == '/') { p++; step = Number(text, ref p, 1, limit); }
            if (p != text.Length) throw Invalid();
            if (step == 0) return 1UL << random.Next(min, limit + 1);
            if (field < 3) flags |= ScheduleFlags.Interval;
            return Range(min + random.Next(step), limit, step);
        }
        ulong bits = 0;
        do
        {
            int low, high;
            bool interval = false;
            if (text[p] is '*' or '?') { p++; low = min; high = max; interval = true; }
            else
            {
                low = Value(text, ref p, field, min, max);
                high = low;
                if (p < text.Length && bits == 0)
                {
                    char suffix = text[p];
                    if (field == 3 && (suffix is 'W' or 'w'))
                    {
                        if (++p != text.Length) throw Invalid();
                        flags |= ScheduleFlags.NearestWeekday;
                        return 1UL << low;
                    }
                    if (field == 5 && (suffix is 'L' or 'l' or '#'))
                    {
                        p++;
                        if (suffix == '#') { flags |= ScheduleFlags.NthWeekday; nth = (byte)Number(text, ref p, 1, 5); }
                        else flags |= ScheduleFlags.LastWeekday;
                        if (p != text.Length) throw Invalid();
                        return 1UL << low;
                    }
                }
                if (p < text.Length && text[p] == '-')
                {
                    p++;
                    high = Value(text, ref p, field, min, max);
                    interval = true;
                }
                else if (p < text.Length && text[p] == '/') high = max;
            }
            int step = 1;
            if (p < text.Length && text[p] == '/') { p++; step = Number(text, ref p, 1, max); interval = true; }
            if (interval && field < 3) flags |= ScheduleFlags.Interval;
            // Sunday aliases must have the same step origin. Do not reproduce Cronos 0.13's 7-x bug.
            if (field == 5 && low == 7 && high < 7) low = 0;
            if (high >= low) bits |= Range(low, high, step);
            else
            {
                int wrapMax = field == 5 ? 6 : max;
                int length = wrapMax - low + 1 + high - min;
                for (int i = 0; i <= length; i += step) bits |= 1UL << (min + (low - min + i) % (wrapMax - min + 1));
            }
            if (p == text.Length) return bits;
            if (text[p++] != ',' || p == text.Length) throw Invalid();
        } while (true);
    }

    private static ulong Range(int low, int high, int step)
    {
        if (low > high) return 0;
        return FieldBits.Range(low, high, step);
    }

    private static int Value(ReadOnlySpan<char> text, ref int p, int field, int min, int max)
    {
        if (p < text.Length && char.IsAsciiDigit(text[p])) return Number(text, ref p, min, max);
        if (field is 4 or 5 && FieldBits.TryName(text, ref p, field == 4, out int value)) return value;
        throw Invalid();
    }
    private static int Number(ReadOnlySpan<char> text, ref int p, int min, int max)
    {
        int value = 0, start = p;
        while (p < text.Length && (uint)(text[p] - '0') <= 9)
        {
            if (p - start == 2) throw Invalid();
            value = value * 10 + text[p++] - '0';
        }
        if (p == start || value < min || value > max) throw Invalid();
        return value;
    }

    private static FormatException Invalid() => new("Invalid cron expression, field value, range, or step.");
}
