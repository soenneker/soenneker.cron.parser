using System;
using TUnit.Core;

namespace Soenneker.Cron.Parser.Tests;

public sealed class CronParserTests
{
    [Test]
    public void DenseScheduleAdvancesAcrossMinuteHourAndDayBoundaries()
    {
        var schedule = CronParser.Parse("* * * * * *", includeSeconds: true);
        foreach (var after in new[]
        {
            new DateTimeOffset(2026, 9, 4, 12, 34, 59, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 4, 12, 59, 59, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 4, 23, 59, 59, TimeSpan.Zero)
        })
            if (schedule.Next(after.AddMilliseconds(500)) != after.AddSeconds(1))
                throw new Exception("Dense schedule did not advance to the next second");
    }

    [Test]
    public void UsesUtcByDefaultAndSupportsSeconds()
    {
        var after = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        if (CronParser.Parse("0 9 * * *").Next(after) != new DateTimeOffset(2026, 9, 5, 9, 0, 0, TimeSpan.Zero))
            throw new Exception("Wrong next daily occurrence");
        if (CronParser.Parse("*/15 * * * * *", includeSeconds: true).Next(after) != after.AddSeconds(15))
            throw new Exception("Seconds or strict next occurrence ignored");
        if (CronParser.Parse("0 0 30 2 *").Next(after) is not null)
            throw new Exception("Impossible date produced an occurrence");
    }

    [Test]
    public void DailyLocalTimeTracksDaylightSavingWithoutRepeatingFallOccurrence()
    {
        var daily = CronParser.Parse("0 9 * * *", "America/New_York");
        if (daily.Next(new DateTimeOffset(2026, 3, 7, 15, 0, 0, TimeSpan.Zero)) != new DateTimeOffset(2026, 3, 8, 13, 0, 0, TimeSpan.Zero))
            throw new Exception("Daily schedule did not follow DST");
        var fall = CronParser.Parse("30 1 * * *", "America/New_York");
        DateTimeOffset first = fall.Next(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero))!.Value;
        DateTimeOffset next = fall.Next(first)!.Value;
        if (next - first < TimeSpan.FromHours(23)) throw new Exception("Daily job repeated in ambiguous hour");
    }

    [Test]
    public void RejectsInvalidExpressionsAndZones()
    {
        try { _ = CronParser.Parse("not cron"); throw new Exception("Invalid cron accepted"); }
        catch (FormatException) { }
        try { _ = CronParser.Parse("* * * * *", "Flywheel/Invalid"); throw new Exception("Invalid zone accepted"); }
        catch (TimeZoneNotFoundException) { }
    }

    [Test]
    public void SupportsNamesListsStepsAndSundayAlias()
    {
        var after = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        if (CronParser.Parse("0,30 9-17/2 * SEP MON-FRI").Next(after) != after.AddHours(1))
            throw new Exception("Names, lists, or range steps failed");
        if (CronParser.Parse("0 0 * * 7").Next(after) != new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero))
            throw new Exception("Sunday alias failed");
        if (CronParser.Parse("0 0 * * FRI-MON").Next(after) != new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.Zero))
            throw new Exception("Wrapped weekday range failed");
    }

    [Test]
    public void HandlesLeapDaysFractionalSecondsAndUpperBoundary()
    {
        var after = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        if (CronParser.Parse("0 0 29 FEB *").Next(after) != new DateTimeOffset(2028, 2, 29, 0, 0, 0, TimeSpan.Zero))
            throw new Exception("Leap day failed");
        if (CronParser.Parse("* * * * * *", includeSeconds: true).Next(after.AddMilliseconds(500)) != after.AddSeconds(1))
            throw new Exception("Fractional instant failed");
        if (CronParser.Parse("* * * * *").Next(DateTimeOffset.MaxValue) is not null)
            throw new Exception("Upper date boundary failed");
    }

    [Test]
    public void MovesNonexistentLocalTimesToStartOfDaylightSaving()
    {
        var next = CronParser.Parse("30 2 * * *", "America/New_York")
            .Next(new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero));
        if (next != new DateTimeOffset(2026, 3, 8, 7, 0, 0, TimeSpan.Zero))
            throw new Exception("Nonexistent local time did not move to the DST transition");
    }

    [Test]
    public void RejectsMalformedAndUnsupportedSyntax()
    {
        foreach (var expression in new[] { "60 * * * *", "*/0 * * * *", "1,,2 * * * *", "* * 0 * *", "* * * 13 *", "* * * * 8", "1/ * * * *" })
        {
            try { _ = CronParser.Parse(expression); }
            catch (FormatException) { continue; }
            throw new Exception($"Invalid expression accepted: {expression}");
        }
    }
}
