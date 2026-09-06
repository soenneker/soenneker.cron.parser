using System;
using System.Runtime.CompilerServices;

namespace Soenneker.Cron.Parser;

internal static class FieldBits
{
    // Bit N represents value N in a cron field. No precomputed step tables are needed.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Range(int first, int last, int step)
    {
        if (first == last) return 1UL << first;
        if (step == 1) return (1UL << (last + 1)) - (1UL << first);
        ulong values = 0;
        for (int value = first; value <= last; value += step)
            values |= 1UL << value;
        return values;
    }

    internal static bool TryName(ReadOnlySpan<char> text, ref int position, bool month, out int value)
    {
        value = -1;
        if (text.Length - position < 3) return false;
        var name = text.Slice(position, 3);
        // Character patterns make both accepted spellings visible and avoid culture conversion.
        value = month ? name switch
        {
            ['J' or 'j', 'A' or 'a', 'N' or 'n'] => 1,
            ['F' or 'f', 'E' or 'e', 'B' or 'b'] => 2,
            ['M' or 'm', 'A' or 'a', 'R' or 'r'] => 3,
            ['A' or 'a', 'P' or 'p', 'R' or 'r'] => 4,
            ['M' or 'm', 'A' or 'a', 'Y' or 'y'] => 5,
            ['J' or 'j', 'U' or 'u', 'N' or 'n'] => 6,
            ['J' or 'j', 'U' or 'u', 'L' or 'l'] => 7,
            ['A' or 'a', 'U' or 'u', 'G' or 'g'] => 8,
            ['S' or 's', 'E' or 'e', 'P' or 'p'] => 9,
            ['O' or 'o', 'C' or 'c', 'T' or 't'] => 10,
            ['N' or 'n', 'O' or 'o', 'V' or 'v'] => 11,
            ['D' or 'd', 'E' or 'e', 'C' or 'c'] => 12,
            _ => -1
        } : name switch
        {
            ['S' or 's', 'U' or 'u', 'N' or 'n'] => 0,
            ['M' or 'm', 'O' or 'o', 'N' or 'n'] => 1,
            ['T' or 't', 'U' or 'u', 'E' or 'e'] => 2,
            ['W' or 'w', 'E' or 'e', 'D' or 'd'] => 3,
            ['T' or 't', 'H' or 'h', 'U' or 'u'] => 4,
            ['F' or 'f', 'R' or 'r', 'I' or 'i'] => 5,
            ['S' or 's', 'A' or 'a', 'T' or 't'] => 6,
            _ => -1
        };
        if (value < 0) return false;
        position += 3;
        return true;
    }
}
