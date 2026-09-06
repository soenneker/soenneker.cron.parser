<div align="center">

# Soenneker.Cron.Parser

**The fastest .NET cron parser.**

[![NuGet](https://img.shields.io/nuget/v/Soenneker.Cron.Parser.svg)](https://www.nuget.org/packages/Soenneker.Cron.Parser/)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)

</div>

An independent .NET cron parser built for services that evaluate schedules
frequently. Parse once into an immutable `CronSchedule`, then calculate UTC
occurrences without allocating schedule objects or temporary collections.

- **Value-type schedules** with no per-parse heap allocation for unseeded input.
- **Five or six fields**, names, ranges, lists, special days, macros, and seeded jitter.
- **Time-zone-aware scheduling**, including skipped and repeated local times.
- **String and span input**, plus caller-owned buffers for bulk results.
- **Measured against four other parsers** with BenchmarkDotNet.

```shell
dotnet add package Soenneker.Cron.Parser
```

## Quick start

```csharp
using Soenneker.Cron.Parser;

var schedule = CronParser.Parse("0 9 * * MON-FRI", "America/New_York");
DateTimeOffset? next = schedule.Next(DateTimeOffset.UtcNow);

var frequent = CronParser.Parse("*/15 * * * * *", includeSeconds: true);
DateTime? nextUtc = frequent.GetNextOccurrence(DateTime.UtcNow);

if (CronParser.TryParse("0 0 LW * *", out var monthEnd))
    next = monthEnd.Next(DateTimeOffset.UtcNow);
```

Results are strictly after the input by default. Use `inclusive: true` to allow
an exact match. `DateTime` inputs must have UTC kind; `DateTimeOffset` accepts
any offset. Both APIs return UTC results, or `null` when no occurrence exists.

## Syntax

| Capability | Example |
|---|---|
| Standard five-field schedule | `0 9 * * *` |
| Optional leading seconds | `*/15 * * * * *` |
| Lists and steps | `3,17,41 1,9,17 * * *` |
| Names and ranges | `0 9 * JAN-MAR MON-FRI` |
| Ranges that wrap | `0 23-1 * DEC-FEB FRI-MON` |
| Wildcard steps within lists | `*/15,7 * * * *` |
| Last day, with optional offset | `0 0 L-3 * *` |
| Nearest weekday | `0 0 15W * *` |
| Last weekday of the month | `0 0 LW * *` |
| Last or nth named weekday | `0 0 * * FRI#2`, `0 0 * * 5L` |
| Macro | `@daily`, `@hourly`, `@every_second` |
| Seeded jitter | `H/15 * * * *` with `jitterSeed: 42` |

Names are case-insensitive three-letter abbreviations. Sunday is `0` or `7`.
`?` acts as a wildcard. Both day-of-month and day-of-week must match when
restricted. Macros also include `@yearly`, `@annually`, `@monthly`, `@weekly`,
`@midnight`, and `@every_minute`. Seeded macros and `L-nW` are supported.

There is no year field, reverse search, or alternate calendar. Compound
special-day lists such as `15,L` remain unsupported. Invalid syntax throws
`FormatException`; `TryParse` returns false for invalid or null input.
Time-zone lookup errors propagate.

## Time zones and daylight saving

Resolve a zone once when creating many schedules:

```csharp
var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
var schedule = CronParser.Parse("30 2 * * *", zone);
```

- **Spring forward:** a scheduled time inside a gap moves to the first valid
  instant after the gap.
- **Fall back:** interval expressions repeat during the overlap; fixed-time
  expressions run at their first occurrence only.

An interval is identified by a wildcard, range, or step in the seconds, minutes,
or hours field. A list of fixed times is not automatically an interval.
Time-zone results follow the rules installed on the host. Tests include both
real zones and synthetic half-hour transitions.

## Avoid input and output allocations

Parse a slice directly instead of creating a substring. The parser does not
retain the supplied buffer:

```csharp
ReadOnlySpan<char> expression = "0 9 * * MON-FRI".AsSpan();
var schedule = CronParser.Parse(expression);

Span<DateTimeOffset> occurrences = stackalloc DateTimeOffset[32];
int count = schedule.FillOccurrences(DateTimeOffset.UtcNow, occurrences);
// Use occurrences[..count].
```

`CronSchedule` is immutable and safe to reuse across threads. Its default value
is uninitialized; parse a schedule before using it. Boxing a value-type schedule
allocates. Seeded jitter uses standard `Random` and can allocate, as can initial
system time-zone setup and invalid-input exceptions.

## Implementation

Each field uses a bit mask: bit *n* represents allowed value *n*. Searches jump
to the next set bit and use monthly date masks to skip invalid dates. Schedules
restricted only by seconds avoid calendar conversion entirely.

Parsing uses readable loops for steps and lists, explicit character patterns for names,
and a small path for single-character fields. Plain list members set a single bit;
calendar searches decode the starting date once and reuse its components. There are no generated step
lookup tables, packed name encodings, unsafe code, or global expression caches.
The production project has no NuGet dependencies. Cronos, NCrontab, Quartz, and
CronParser are referenced only by the test or benchmark projects.

## Validation and compatibility

```powershell
dotnet test --project test/Soenneker.Cron.Parser.Tests -- --treenode-filter '/*/*/*/*'
dotnet run -c Release --project benchmarks/Soenneker.Cron.Parser.Benchmarks -- --validate
dotnet run -c Release --project benchmarks/Soenneker.Cron.Parser.Benchmarks -- --filter '*ComparisonBenchmarks*'
```

Tests cover randomized expressions, special days, calendar boundaries, fractional
seconds, DST transitions, long lists, and span input. Compatibility tests exclude
known upstream defects: `7-1/2` correctly includes Sunday, `7-3/2` matches
`0-3/2`, and wildcard-step lists work in any position.

See the [Cronos issue and pull-request review](docs/cronos-review.md) for the
reports behind those decisions and intentionally deferred features.
