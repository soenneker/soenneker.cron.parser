using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Cronos;
using NCrontab;
using Soenneker.Cron.Parser;
using SoennekerParser = Soenneker.Cron.Parser.CronParser;
using OtherParser = CronParser.CronExpressionParser;

[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 8)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ComparisonBenchmarks
{
    [Params("EveryMinute", "Every15Seconds", "Weekdays", "LeapDay", "Lists", "Names")]
    public string Scenario { get; set; } = null!;
    private string _expression = null!, _quartzExpression = null!;
    private CronFormat _format;
    private readonly CrontabSchedule.ParseOptions _options = new();
    private CronExpression _cronos = null!;
    private CrontabSchedule _ncrontab = null!;
    private Quartz.CronExpression _quartz = null!;
    private CronParser.CronExpression _other = null!;
    private CronSchedule _schedule;
    private readonly DateTime _from = new(2026, 9, 4, 12, 34, 56, DateTimeKind.Utc);
    private DateTimeOffset _fromOffset;

    [GlobalSetup]
    public void Setup()
    {
        (_expression, _quartzExpression) = Scenario switch
        {
            "EveryMinute" => ("* * * * *", "0 * * * * ?"),
            "Every15Seconds" => ("*/15 * * * * *", "*/15 * * * * ?"),
            "Weekdays" => ("0 9 * * 1-5", "0 0 9 ? * MON-FRI"),
            "LeapDay" => ("0 0 29 2 *", "0 0 0 29 2 ?"),
            "Lists" => ("3,17,41 1,9,17 * * *", "0 3,17,41 1,9,17 * * ?"),
            "Names" => ("0,30 9-17/2 * JAN,MAR,SEP MON-FRI", "0 0,30 9-17/2 ? JAN,MAR,SEP MON-FRI"),
            _ => throw new ArgumentException(Scenario)
        };
        _format = Scenario == "Every15Seconds" ? CronFormat.IncludeSeconds : CronFormat.Standard;
        _options.IncludingSeconds = _format == CronFormat.IncludeSeconds;
        _fromOffset = new DateTimeOffset(_from);
        _cronos = CronosParse();
        _ncrontab = NCrontabParse();
        _quartz = QuartzParse();
        _other = CronParserParse();
        _schedule = SoennekerParse();
        DateTime cursor = _from;
        for (int i = 0; i < 8; i++)
        {
            var expected = _cronos.GetNextOccurrence(cursor);
            var actual = _schedule.GetNextOccurrence(cursor);
            var ncron = _ncrontab.GetNextOccurrence(cursor);
            var quartz = _quartz.GetNextValidTimeAfter(new DateTimeOffset(cursor))?.UtcDateTime;
            var other = _other.GetNextAvailableTime(new DateTimeOffset(cursor))?.UtcDateTime;
            if (actual != expected || ncron != expected || quartz != expected || other != expected)
                throw new Exception($"{Scenario}: Cronos={expected:O}, Soenneker={actual:O}, NCrontab={ncron:O}, Quartz={quartz:O}, CronParser={other:O}");
            cursor = expected!.Value.AddTicks(i % 2);
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Parse")]
    public CronExpression CronosParse() => CronExpression.Parse(_expression, _format);
    [Benchmark, BenchmarkCategory("Parse")]
    public CronSchedule SoennekerParse() => SoennekerParser.Parse(_expression, includeSeconds: _options.IncludingSeconds);
    [Benchmark, BenchmarkCategory("Parse")]
    public CrontabSchedule NCrontabParse() => CrontabSchedule.Parse(_expression, _options);
    [Benchmark, BenchmarkCategory("Parse")]
    public Quartz.CronExpression QuartzParse() => new(_quartzExpression, TimeZoneInfo.Utc);
    [Benchmark, BenchmarkCategory("Parse")]
    public CronParser.CronExpression CronParserParse() => OtherParser.Parse(_expression);

    [Benchmark(Baseline = true), BenchmarkCategory("Next")]
    public DateTime? CronosNext() => _cronos.GetNextOccurrence(_from);
    [Benchmark, BenchmarkCategory("Next")]
    public DateTime? SoennekerNext() => _schedule.GetNextOccurrence(_from);
    [Benchmark, BenchmarkCategory("Next")]
    public DateTime? NCrontabNext() => _ncrontab.GetNextOccurrence(_from);
    [Benchmark, BenchmarkCategory("Next")]
    public DateTime? QuartzNext() => _quartz.GetNextValidTimeAfter(_fromOffset)?.UtcDateTime;
    [Benchmark, BenchmarkCategory("Next")]
    public DateTime? CronParserNext() => _other.GetNextAvailableTime(_fromOffset)?.UtcDateTime;
}
