using System;
using Cronos;
using TUnit.Core;

namespace Soenneker.Cron.Parser.Tests;

public sealed class CompatibilityTests
{
    [Test]
    public void NumericSchedulesMatchCronosAcrossRandomDates()
    {
        var random = new Random(1701);
        for (int i = 0; i < 1500; i++)
        {
            string Field(int min, int max) => random.Next(5) switch
            {
                0 => "*", 1 => $"*/{random.Next(1, max + 1)}",
                2 => $"{random.Next(min, max + 1)},{random.Next(min, max + 1)}",
                3 => $"{min}-{max}/{random.Next(1, max + 1)}",
                _ => random.Next(min, max + 1).ToString()
            };
            bool seconds = i % 2 == 0;
            string expression = (seconds ? Field(0, 59) + " " : "") + $"{Field(0, 59)} {Field(0, 23)} {Field(1, 31)} {Field(1, 12)} {Field(0, 7)}";
            var from = new DateTime(random.Next(1900, 2200), random.Next(1, 13), random.Next(1, 29), random.Next(24), random.Next(60), random.Next(60), DateTimeKind.Utc).AddTicks(random.Next(2) * 12345);
            Compare(expression, seconds, "UTC", from, 8);
        }
    }

    [Test]
    public void ExtendedSyntaxAndMacrosMatchCronos()
    {
        foreach (string expression in new[] { "0 0 L * *", "0 0 L-3 * *", "0 0 LW * *", "0 0 15W * *", "0 0 * * 5L", "0 0 * * MON#2", "0 0 ? * MON", "0 23-01 * DEC-FEB FRI-MON", "0 0 * * 5-1/2", "@daily", "@yearly", "@every_second", "0,30 9-17/2 * SEP MON-FRI", "? ? ? ? ?" })
            Compare(expression, false, "UTC", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 100);
        Compare("H/15 H * * *", false, "UTC", DateTime.UnixEpoch, 50, 42);
        Compare("@daily", false, "UTC", DateTime.UnixEpoch, 50, 42);
    }

    [Test]
    public void DstTransitionsMatchCronosForFixedAndIntervalSchedules()
    {
        foreach (string zone in new[] { "America/New_York", "Europe/Berlin", "Australia/Lord_Howe" })
        foreach (string expression in new[] { "*/15 * * * *", "30 1 * * *", "30 2 * * *", "0 0 L * *" })
        foreach (int month in new[] { 3, 4, 10, 11 })
            Compare(expression, false, zone, new DateTime(2026, month, 1, 0, 0, 0, DateTimeKind.Utc), 3000);
    }

    [Test]
    public void ExactTransitionInstantsMatchCronos()
    {
        foreach (var transition in new[]
        {
            ("America/New_York", new DateTime(2026, 3, 8, 7, 0, 0, DateTimeKind.Utc)),
            ("America/New_York", new DateTime(2026, 11, 1, 6, 0, 0, DateTimeKind.Utc)),
            ("Europe/Berlin", new DateTime(2026, 3, 29, 1, 0, 0, DateTimeKind.Utc)),
            ("Europe/Berlin", new DateTime(2026, 10, 25, 1, 0, 0, DateTimeKind.Utc)),
            ("Australia/Lord_Howe", new DateTime(2026, 4, 4, 15, 0, 0, DateTimeKind.Utc)),
            ("Australia/Lord_Howe", new DateTime(2026, 10, 3, 15, 30, 0, DateTimeKind.Utc))
        })
        foreach (string expression in new[] { "*/13 * * * * *", "0 30 1 * * *", "0 30 2 * * *", "0 0,30 1,2 * * *", "0 0-59/15 1-3 * * *" })
        for (int minute = -90; minute <= 90; minute++)
        {
            Compare(expression, true, transition.Item1, transition.Item2.AddMinutes(minute), 3);
            Compare(expression, true, transition.Item1, transition.Item2.AddMinutes(minute).AddTicks(-1), 3);
        }
    }

    [Test]
    public void SpecialDaysMatchAcrossCalendarCycle()
    {
        foreach (string expression in new[] { "0 0 L * *", "0 0 L-30 * *", "0 0 L-3W * *", "0 0 1W * *", "0 0 31W * *", "0 0 * * 7L", "0 0 * * 1#5", "0 0 15W * MON", "0 0 29 2 1" })
            Compare(expression, false, "UTC", new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), 4800);
    }

    [Test]
    public void InvalidSyntaxHasSameAcceptanceAsCronos()
    {
        foreach (string expression in new[] { "", " ", "* * * *", "* * * * * *", "*/60 * * * *", "0 0 * * */8", "000 * * * *", "1,,2 * * * *", "1/ * * * *", "*\n* * * *", "0 0 L-40 * *", "0 0 * * MON#6", "@bad", "60 * * * *", "H * * * *", "0 0 * 0 *" })
        {
            bool expected = CronExpression.TryParse(expression, out _);
            bool actual;
            try { _ = CronParser.Parse(expression); actual = true; }
            catch (FormatException) { actual = false; }
            if (actual != expected) throw new Exception($"Acceptance differs for {expression}");
        }
    }

    [Test]
    public void BoundariesAndInclusiveSearchMatchCronos()
    {
        foreach (string expression in new[] { "* * * * *", "0 0 29 2 *", "0 0 30 2 *", "0 0 1 1 *", "0 0 * * 7" })
        foreach (DateTime from in new[] { DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc), DateTime.UnixEpoch })
            Compare(expression, false, "UTC", from, 3);
    }

    [Test]
    public void ReversedWeekdayStepsMatchCronos()
    {
        for (int low = 0; low <= 7; low++)
        for (int high = 0; high <= 7; high++)
        for (int step = 1; step <= 7; step++)
            // Cronos 0.13 has a known Sunday-alias bug; independent regression tests cover the corrected semantics.
            if (low != 7 || high == 7) Compare($"0 0 * * {low}-{high}/{step}", false, "UTC", DateTime.UnixEpoch, 4);
    }

    [Test]
    public void PublicValidationAndAssemblyIsolation()
    {
        if (CronParser.TryParse(null, out _) || CronParser.TryParse("invalid", out _)) throw new Exception("Invalid input accepted");
        if (!CronParser.TryParse("0 9 * * *", out var schedule)) throw new Exception("Valid input rejected");
        try { schedule.GetNextOccurrence(DateTime.SpecifyKind(DateTime.UnixEpoch, DateTimeKind.Local)); throw new Exception("Local DateTime accepted"); }
        catch (ArgumentException) { }
        foreach (var reference in typeof(CronSchedule).Assembly.GetReferencedAssemblies())
            if (reference.Name == "Cronos") throw new Exception("Production assembly depends on Cronos");
    }

    [Test]
    public void BulkResultsAndOffsetsAreCorrect()
    {
        var schedule = CronParser.Parse("*/15 * * * * *", includeSeconds: true);
        Span<DateTimeOffset> results = stackalloc DateTimeOffset[100];
        var from = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(5));
        int count = schedule.FillOccurrences(from, results, true);
        if (count != 100) throw new Exception("Wrong count");
        for (int i = 0; i < count; i++)
            if (results[i] != from.AddSeconds(i * 15) || results[i].Offset != TimeSpan.Zero) throw new Exception("Wrong bulk occurrence");
        try { default(CronSchedule).Next(from); throw new Exception("Default schedule accepted"); }
        catch (InvalidOperationException) { }
    }

    private static void Compare(string expression, bool seconds, string zoneId, DateTime from, int count, int? seed = null)
    {
        CronFormat format = seconds ? CronFormat.IncludeSeconds : CronFormat.Standard;
        CronExpression expected = seed is { } s ? CronExpression.Parse(expression, format, s) : CronExpression.Parse(expression, format);
        var actual = CronParser.Parse(expression, zoneId, seconds, seed);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        for (int i = 0; i < count; i++)
        {
            bool inclusive = i % 2 == 0;
            DateTime? next = expected.GetNextOccurrence(from, zone, inclusive);
            DateTime? result = actual.GetNextOccurrence(from, inclusive);
            if (next != result) throw new Exception($"{expression} ({zoneId}) at {from:O}, inclusive={inclusive}: expected {next:O}, got {result:O}");
            if (next is null || next.Value.Ticks > DateTime.MaxValue.Ticks - TimeSpan.TicksPerSecond) break;
            from = next.Value.AddTicks(i % 2 == 0 ? 1 : 0);
        }
    }
}
