using System;
using System.Runtime.CompilerServices;

namespace Soenneker.Cron.Parser;

// Handles ordinary fields in one pass. Special day modifiers, macros, and jitter use ExpressionParser.
internal static class FastParser
{
    internal static bool TryParse(ReadOnlySpan<char> text, bool seconds, Span<ulong> fields, out ScheduleFlags flags)
    {
        flags = ScheduleFlags.None;
        int position = 0;
        fields[0] = 1;
        return (!seconds || Field(text, ref position, 0, 59, true, false, out fields[0], ref flags))
            && Field(text, ref position, 0, 59, true, false, out fields[1], ref flags)
            && Field(text, ref position, 0, 23, true, false, out fields[2], ref flags)
            && Field(text, ref position, 1, 31, false, true, out fields[3], ref flags)
            && Field(text, ref position, 1, 12, false, false, out fields[4], ref flags)
            && Field(text, ref position, 0, 7, false, false, out fields[5], ref flags)
            && position == text.Length + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Field(ReadOnlySpan<char> text, ref int position, int minimum, int maximum,
        bool definesInterval, bool dayOfMonth, out ulong values, ref ScheduleFlags flags)
    {
        // Most cron fields are one character. Keep this path small enough to inline;
        // ranges, lists, and names remain in the ordinary loop below.
        if (position < text.Length && (position + 1 == text.Length || text[position + 1] == ' '))
        {
            char token = text[position];
            if (token is '*' or '?' || (dayOfMonth && token == 'L'))
            {
                values = (1UL << (maximum + 1)) - (1UL << minimum);
                if (token == 'L') flags |= ScheduleFlags.LastDay;
                else if (definesInterval) flags |= ScheduleFlags.Interval;
                position += 2;
                return true;
            }
            int digit = token - '0';
            if ((uint)digit <= 9 && digit >= minimum && digit <= maximum)
            {
                values = 1UL << digit;
                position += 2;
                return true;
            }
        }
        return ComplexField(text, ref position, minimum, maximum, definesInterval, out values, ref flags);
    }

    private static bool ComplexField(ReadOnlySpan<char> text, ref int position, int minimum, int maximum,
        bool definesInterval, out ulong values, ref ScheduleFlags flags)
    {
        values = 0;
        while (position < text.Length)
        {
            char firstCharacter = text[position];
            int first, last;
            if (firstCharacter is '*' or '?')
            {
                position++;
                first = minimum;
                last = maximum;
                if (definesInterval) flags |= ScheduleFlags.Interval;
            }
            else
            {
                if (!Value(text, ref position, minimum, maximum, out first)) return false;
                // Plain list members and final values need no range or step processing.
                if (position < text.Length && text[position] == ',')
                {
                    values |= 1UL << first;
                    position++;
                    continue;
                }
                if (position == text.Length || text[position] == ' ')
                {
                    values |= 1UL << first;
                    position++;
                    return true;
                }
                last = first;
                if (position < text.Length && text[position] == '-')
                {
                    position++;
                    if (!Value(text, ref position, minimum, maximum, out last) || last < first) return false;
                    if (definesInterval) flags |= ScheduleFlags.Interval;
                }
                else if (position < text.Length && text[position] == '/') last = maximum;
            }

            int step = 1;
            if (position < text.Length && text[position] == '/')
            {
                position++;
                if (!Number(text, ref position, out step) || step < 1 || step > maximum) return false;
                if (definesInterval) flags |= ScheduleFlags.Interval;
            }
            values |= FieldBits.Range(first, last, step);

            if (position < text.Length && text[position] == ',')
            {
                position++;
                continue;
            }
            if (position < text.Length && text[position] != ' ') return false;
            // One-past-end distinguishes a complete final field from a missing field.
            position++;
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Value(ReadOnlySpan<char> text, ref int position, int minimum, int maximum, out int value)
    {
        if (Number(text, ref position, out value)) return value >= minimum && value <= maximum;
        bool month = minimum == 1 && maximum == 12;
        return (month || maximum == 7) && FieldBits.TryName(text, ref position, month, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Number(ReadOnlySpan<char> text, ref int position, out int value)
    {
        value = 0;
        int cursor = position;
        if ((uint)cursor >= (uint)text.Length) return false;
        int digit = text[cursor] - '0';
        if ((uint)digit > 9) return false;
        int number = digit;
        cursor++;
        if (cursor < text.Length)
        {
            digit = text[cursor] - '0';
            if ((uint)digit <= 9)
            {
                number = number * 10 + digit;
                cursor++;
            }
        }
        position = cursor;
        value = number;
        return true;
    }
}
