using System;
using Cronos;
using TUnit.Core;

namespace Soenneker.Cron.Parser.Tests;

public sealed class OptimizationTests
{
    [Test]
    public void SecondOnlyShortcutMatchesCronos()
    {
        foreach (string field in new[] { "*", "*/2", "*/15", "*/59", "0", "59", "1,59", "0,58", "5-55/7" })
        {
            string expression = field + " * * * * *";
            var schedule = CronParser.Parse(expression, includeSeconds: true);
            var oracle = CronExpression.Parse(expression, CronFormat.IncludeSeconds);
            foreach (var start in new[] { DateTime.UnixEpoch, new DateTime(2026, 12, 31, 23, 59, 58, DateTimeKind.Utc), new DateTime(9999, 12, 31, 23, 59, 58, DateTimeKind.Utc) })
            for (int second = 0; second < 2; second++)
            foreach (int fraction in new[] { 0, 1, 9999999 })
            foreach (bool inclusive in new[] { false, true })
            {
                DateTime from = start.AddSeconds(second).AddTicks(fraction);
                // Cronos itself can throw at its upper date boundary. For these date-unrestricted
                // expressions, evaluate one year earlier and project only representable results.
                DateTime? expected = oracle.GetNextOccurrence(from.Year == 9999 ? from.AddYears(-1) : from, inclusive);
                if (from.Year == 9999 && expected.HasValue) expected = expected.Value.Year == 9999 ? null : expected.Value.AddYears(1);
                if (schedule.GetNextOccurrence(from, inclusive) != expected) throw new Exception($"Second shortcut failed: {expression}, {from:O}");
            }
        }
    }

    [Test]
    public void SpanInputIsNotRetained()
    {
        char[] buffer = "prefix0,30 9 * * MON-FRIsuffix".ToCharArray();
        var parsed = CronParser.Parse(buffer.AsSpan(6, buffer.Length - 12));
        var expected = CronParser.Parse("0,30 9 * * MON-FRI");
        Array.Fill(buffer, 'x');
        if (parsed.Next(DateTimeOffset.UnixEpoch) != expected.Next(DateTimeOffset.UnixEpoch)) throw new Exception("Span input was retained or parsed incorrectly");
    }

    [Test]
    public void NamedFieldsAndNumericListsMatchSlowPath()
    {
        // Leading whitespace selects the general parser. Both paths must interpret the same grammar.
        foreach (string expression in new[] { "0,30 9-17/2 * JAN,MAR,SEP MON-FRI", "1,2,3 0,12,23 * * *", "0 0 * jan-dec/2 sun-sat", "*/7,59 * * * *", "0 0 L * *", "0 0 * * 7-3/2" })
        {
            var fast = CronParser.Parse(expression);
            var general = CronParser.Parse(" " + expression);
            var from = DateTimeOffset.UnixEpoch;
            for (int i = 0; i < 100; i++)
            {
                var expected = general.Next(from);
                if (fast.Next(from) != expected) throw new Exception($"Parser paths disagree: {expression}");
                from = expected!.Value;
            }
        }
    }
    [Test]
    public void EveryNameAcceptsAllAsciiCaseCombinations()
    {
        string[] months = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];
        string[] weekdays = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];
        foreach (bool month in new[] { false, true })
        {
            string[] names = month ? months : weekdays;
            for (int index = 0; index < names.Length; index++)
            for (int casing = 0; casing < 8; casing++)
            {
                char[] letters = names[index].ToCharArray();
                for (int letter = 0; letter < 3; letter++)
                    if ((casing & (1 << letter)) != 0) letters[letter] = char.ToLowerInvariant(letters[letter]);
                string name = new(letters);
                string expression = month ? $"0 0 1 {name} *" : $"0 0 * * {name}";
                string numeric = month ? $"0 0 1 {index + 1} *" : $"0 0 * * {index}";
                var parsed = CronParser.Parse(expression);
                var expected = CronParser.Parse(numeric);
                DateTime cursor = DateTime.UnixEpoch;
                for (int occurrence = 0; occurrence < 3; occurrence++)
                {
                    DateTime? next = expected.GetNextOccurrence(cursor);
                    if (parsed.GetNextOccurrence(cursor) != next) throw new Exception($"Incorrect name: {expression}");
                    cursor = next!.Value;
                }
            }
        }
        foreach (string invalid in new[] { "JAX", "JUNX", "MOR", "SE", "ＳＥＰ" })
            if (CronParser.TryParse($"0 0 * {invalid} *", out _)) throw new Exception($"Invalid name accepted: {invalid}");
    }
}
