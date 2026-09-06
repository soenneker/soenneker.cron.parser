using System;
using TUnit.Core;

namespace Soenneker.Cron.Parser.Tests;

public sealed class UpstreamRegressionTests
{
    [Test]
    public void SundayAliasPreservesStepPhase()
    {
        var from = new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.Zero);
        for (int end = 0; end < 7; end++)
        for (int step = 1; step <= 7; step++)
        {
            var alias = CronParser.Parse($"0 0 * * 7-{end}/{step}");
            var canonical = CronParser.Parse($"0 0 * * 0-{end}/{step}");
            var cursor = from;
            for (int i = 0; i < 20; i++)
            {
                var expected = canonical.Next(cursor);
                var actual = alias.Next(cursor);
                if (actual != expected) throw new Exception($"7-{end}/{step}: expected {expected}, got {actual}");
                cursor = expected!.Value;
            }
        }
    }

    [Test]
    public void WildcardListsAreUnionsInAnyPosition()
    {
        foreach (var pair in new[]
        {
            ("*/15,7 * * * *", "0,7,15,30,45 * * * *"),
            ("7,*/15 * * * *", "0,7,15,30,45 * * * *"),
            ("0 0 * * */2,3", "0 0 * * 0,2,3,4,6"),
            ("0 0 */10,5 * *", "0 0 1,5,11,21,31 * *"),
            ("0 0 * */4,2 *", "0 0 * 1,2,5,9 *"),
            ("1,* * * * *", "* * * * *"),
            ("?,1 * * * *", "* * * * *")
        })
        {
            var schedule = CronParser.Parse(pair.Item1);
            var equivalent = CronParser.Parse(pair.Item2);
            var cursor = DateTimeOffset.UnixEpoch;
            for (int i = 0; i < 100; i++)
            {
                var next = equivalent.Next(cursor);
                if (schedule.Next(cursor) != next) throw new Exception($"Incorrect list union: {pair.Item1}");
                cursor = next!.Value;
            }
        }
        foreach (string invalid in new[] { "*/,1 * * * *", "*/0,1 * * * *", "1,,* * * * *", "*, * * * *", "0 0 1,L * *", "0 0 * * 1,2#3" })
            if (CronParser.TryParse(invalid, out _)) throw new Exception($"Invalid list accepted: {invalid}");
    }

    [Test]
    public void LowerBoundaryDoesNotSkipFirstRepresentableLocalOccurrence()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Regression-05", TimeSpan.FromHours(-5), "Regression-05", "Regression-05");
        var schedule = CronParser.Parse("0 0 * * *", zone);
        var expected = DateTimeOffset.MinValue.AddHours(5);
        foreach (var from in new[] { DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddTicks(1), expected.AddTicks(-1) })
            if (schedule.Next(from) != expected) throw new Exception("First representable local midnight was skipped");
        if (schedule.Next(expected) != expected.AddDays(1)) throw new Exception("Exclusive search repeated midnight");
    }

    [Test]
    public void UpperBoundaryReturnsNullWithoutThrowing()
    {
        foreach (int offset in new[] { -14, -5, 0, 5, 14 })
        {
            var zone = TimeZoneInfo.CreateCustomTimeZone($"Boundary{offset}", TimeSpan.FromHours(offset), "Boundary", "Boundary");
            foreach (string expression in new[] { "0 0 * * *", "0 19 31 12 *", "* * * * *" })
                if (CronParser.Parse(expression, zone).Next(DateTimeOffset.MaxValue) is not null) throw new Exception("Occurrence exceeded upper boundary");
        }
    }

    [Test]
    public void VeryLongListsUseConstantStackSpace()
    {
        // Do not send this input to Cronos 0.13.0: its recursive list parser can terminate the test process.
        string expression = "0 0 * * " + string.Concat(System.Linq.Enumerable.Repeat("1,", 100_000)) + "1";
        var schedule = CronParser.Parse(expression);
        var expected = CronParser.Parse("0 0 * * 1");
        if (schedule.Next(DateTimeOffset.UnixEpoch) != expected.Next(DateTimeOffset.UnixEpoch)) throw new Exception("Long list changed semantics");
        if (CronParser.TryParse(expression + ",", out _)) throw new Exception("Trailing comma accepted");
    }

    [Test]
    public void SyntheticHalfHourTransitionsHaveStableExpectations()
    {
        // Fixed rules isolate scheduling semantics from changes to the host's IANA/Windows database (#91).
        var start = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 6, 1);
        var end = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 10, 1);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), TimeSpan.FromMinutes(30), start, end);
        var zone = TimeZoneInfo.CreateCustomTimeZone("SyntheticHalfHour", TimeSpan.Zero, "Synthetic", "Standard", "Daylight", [rule]);
        var fixedTime = CronParser.Parse("15 2 * * *", zone);
        var gapStart = new DateTimeOffset(2026, 6, 1, 2, 0, 0, TimeSpan.Zero);
        if (fixedTime.Next(gapStart.AddTicks(-1)) != gapStart) throw new Exception("Gap was not shifted to its first valid instant");
        var interval = CronParser.Parse("*/15 * * * *", zone);
        var overlapStart = new DateTimeOffset(2026, 10, 1, 1, 30, 0, TimeSpan.Zero);
        if (interval.Next(overlapStart.AddTicks(-1)) != overlapStart) throw new Exception("Interval missed repeated wall time");
        var once = CronParser.Parse("45 1 * * *", zone);
        var first = new DateTimeOffset(2026, 10, 1, 1, 15, 0, TimeSpan.Zero);
        if (once.Next(first) != new DateTimeOffset(2026, 10, 2, 1, 45, 0, TimeSpan.Zero)) throw new Exception("Fixed time repeated in overlap");
    }

    [Test]
    public void EmptyTryParseAndFractionalStartsAreSafe()
    {
        foreach (string? input in new string?[] { null, "", " ", "\t", "\r\n" })
            if (CronParser.TryParse(input, out _)) throw new Exception("Blank input accepted");
        var schedule = CronParser.Parse("* * * * * *", includeSeconds: true);
        var second = DateTimeOffset.UnixEpoch;
        foreach (long ticks in new[] { 1L, 9999L, 9_999_999L })
            foreach (bool inclusive in new[] { false, true })
                if (schedule.Next(second.AddTicks(ticks), inclusive) != second.AddSeconds(1)) throw new Exception("Search returned a past occurrence");
    }
}
