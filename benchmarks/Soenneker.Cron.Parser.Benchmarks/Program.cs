using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Cronos;
using Soenneker.Cron.Parser;
using SoennekerParser = Soenneker.Cron.Parser.CronParser;

if (args.Contains("--validate"))
{
    foreach (string scenario in new[] { "EveryMinute", "Every15Seconds", "Weekdays", "LeapDay", "Lists", "Names" })
    {
        new ComparisonBenchmarks { Scenario = scenario }.Setup();
        Console.WriteLine($"Validated {scenario}");
    }
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

[MemoryDiagnoser]
[ShortRunJob]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ParserBenchmarks
{
    [Params("* * * * *", "*/15 * * * * *", "0 9 * * 1-5", "0 0 29 2 *", "0 0 L * *")]
    public string Expression { get; set; } = null!;
    private CronExpression _cronos = null!;
    private CronSchedule _schedule;
    private readonly DateTime _from = new(2026, 9, 4, 12, 34, 56, DateTimeKind.Utc);
    private bool Seconds => Expression == "*/15 * * * * *";

    [GlobalSetup]
    public void Setup()
    {
        _cronos = CronExpression.Parse(Expression, Seconds ? CronFormat.IncludeSeconds : CronFormat.Standard);
        _schedule = SoennekerParser.Parse(Expression, includeSeconds: Seconds);
        if (_cronos.GetNextOccurrence(_from) != _schedule.GetNextOccurrence(_from)) throw new Exception("Benchmark results differ");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Parse")]
    public CronExpression CronosParse() => CronExpression.Parse(Expression, Seconds ? CronFormat.IncludeSeconds : CronFormat.Standard);
    [Benchmark, BenchmarkCategory("Parse")]
    public CronSchedule SoennekerParse() => SoennekerParser.Parse(Expression, includeSeconds: Seconds);
    [Benchmark(Baseline = true), BenchmarkCategory("Next")]
    public DateTime? CronosNext() => _cronos.GetNextOccurrence(_from);
    [Benchmark, BenchmarkCategory("Next")]
    public DateTime? SoennekerNext() => _schedule.GetNextOccurrence(_from);
}


[MemoryDiagnoser]
[ShortRunJob]
public class ZonedBenchmarks
{
    [Params("Ordinary", "SpringGap", "FallOverlap")]
    public string Scenario { get; set; } = null!;
    private CronExpression _cronos = null!;
    private CronSchedule _schedule;
    private TimeZoneInfo _zone = null!;
    private DateTime _from;

    [GlobalSetup]
    public void Setup()
    {
        _zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        string expression = Scenario == "FallOverlap" ? "*/15 * * * *" : "30 2 * * *";
        _from = Scenario switch
        {
            "SpringGap" => new DateTime(2026, 3, 8, 6, 59, 59, DateTimeKind.Utc),
            "FallOverlap" => new DateTime(2026, 11, 1, 5, 59, 59, DateTimeKind.Utc),
            _ => new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc)
        };
        _cronos = CronExpression.Parse(expression);
        _schedule = SoennekerParser.Parse(expression, _zone);
        if (_cronos.GetNextOccurrence(_from, _zone) != _schedule.GetNextOccurrence(_from)) throw new Exception("Benchmark results differ");
    }

    [Benchmark(Baseline = true)]
    public DateTime? CronosNext() => _cronos.GetNextOccurrence(_from, _zone);
    [Benchmark]
    public DateTime? SoennekerNext() => _schedule.GetNextOccurrence(_from);
}
