# Benchmark results

The current comparison measures five libraries across six equivalent UTC schedules,
with separate parsing and next-occurrence benchmarks (60 cases total).

- [Complete BenchmarkDotNet report](reports/comparison.md), including errors and standard deviations
- [Machine-readable results](reports/comparison.csv)
- [Summary tables](../README.md#benchmarks)

## Environment and method

Measured September 5, 2026 (local time) on Windows 11, AMD Ryzen Threadripper PRO
9995WX, .NET SDK 10.0.400 / runtime 10.0.11, BenchmarkDotNet 0.15.8. Release builds,
one launch, three warmup iterations, eight measurement iterations, MemoryDiagnoser.
Competitors: Cronos 0.13.0, NCrontab 3.4.0, Quartz 4.0.0, CronParser 1.3.0.

Parsing creates a new schedule on each invocation; there is no expression cache.
Next-occurrence benchmarks reuse a parsed schedule and independently search after
2026-09-04 12:34:56 UTC. BenchmarkDotNet consumes returned results. Time zones and
NCrontab parse options are prepared before timing. Quartz receives equivalent
expressions because its seconds and day-of-week conventions differ.

Every scenario validates eight consecutive occurrences across all five libraries
before timing, using exact and fractional starting instants. This establishes
agreement for these workloads, not complete cross-library semantic equivalence.

| Scenario | Common expression | Equivalent Quartz expression |
|---|---|---|
| EveryMinute | `* * * * *` | `0 * * * * ?` |
| Every15Seconds | `*/15 * * * * *` | `*/15 * * * * ?` |
| Weekdays | `0 9 * * 1-5` | `0 0 9 ? * MON-FRI` |
| LeapDay | `0 0 29 2 *` | `0 0 0 29 2 ?` |
| Lists | `3,17,41 1,9,17 * * *` | `0 3,17,41 1,9,17 * * ?` |
| Names | `0,30 9-17/2 * JAN,MAR,SEP MON-FRI` | `0 0,30 9-17/2 ? JAN,MAR,SEP MON-FRI` |

## Interpretation

Lower mean time is better. Allocated bytes measure managed allocation per operation;
zero does not mean a schedule occupies no memory. Soenneker stores its schedule as
a value type. Boxing, invalid-input exceptions, seeded jitter, and initial time-zone
setup can still allocate.

The README highlights the lowest measured mean in each row, not statistical
significance. Consult the full report's error and standard deviation columns before
interpreting small differences. One machine and six expressions cannot establish
that any library is fastest across all .NET workloads. This suite excludes invalid
input, seeded jitter, bulk output, and special-day expressions unsupported by some
competitors. The production project has no package references.

## Optimizations in this revision

Calendar searches deconstruct the starting date once and reuse its components.
Plain list members set one bit without going through range and step handling.
Numeric parsing reads each character once, and month/weekday names use explicit
ASCII character patterns with mixed-case coverage. Single-character `L` stays
on the small field path; compound day modifiers use the general parser.

Stepped ranges still use ordinary integer loops. There are no generated step
tables, packed name encodings, unsafe accesses, or expression caches. These
changes apply to field shapes across expressions, not specific benchmark strings.
## Reproduce

Run from the repository root with no competing benchmark or build workload:

```powershell
dotnet run -c Release --project benchmarks/Soenneker.Cron.Parser.Benchmarks -- --validate
dotnet run -c Release --project benchmarks/Soenneker.Cron.Parser.Benchmarks -- --filter '*ComparisonBenchmarks*' --artifacts benchmarks/results/five-libraries
Copy-Item benchmarks/results/five-libraries/results/ComparisonBenchmarks-report-github.md benchmarks/reports/comparison.md
Copy-Item benchmarks/results/five-libraries/results/ComparisonBenchmarks-report.csv benchmarks/reports/comparison.csv
./benchmarks/UpdateReadme.ps1
```

The README script checks for all 60 rows before replacing its tables. Update the
snapshot environment/version text in the script when publishing a run from a
different environment or dependency version.

## Additional current comparisons

The existing parser and time-zone suites were also rerun on this revision with
three warmup and eight measurement iterations. Their job name remains `ShortRun`,
but the command-line iteration override is recorded in each report. Together with
the five-library suite, all 86 measured cases completed. Soenneker has the lowest
mean in each comparison and zero measured allocation in all its cases.

| Operation | Soenneker mean / allocation | Cronos mean / allocation |
|---|---:|---:|
| Parse month end (`0 0 L * *`) | 8.221 ns / 0 B | 10.601 ns / 48 B |
| Next month end | 12.808 ns / 0 B | 18.814 ns / 0 B |
| Ordinary New York occurrence | 127.8 ns / 0 B | 145.2 ns / 0 B |
| Spring-forward gap | 598.0 ns / 0 B | 649.4 ns / 0 B |
| Fall-back interval overlap | 624.1 ns / 0 B | 1,303.7 ns / 40 B |

The additional parser suite also checks the four common schedules with its original
call shape; those results all have lower Soenneker means. This repeats some workloads
from the five-library suite, so 86 cases should not be interpreted as 86 distinct
schedule scenarios. These are measured means, not a guarantee for untested workloads.

- [Current parser report](reports/parser-current.md) and [CSV](reports/parser-current.csv)
- [Current time-zone report](reports/time-zones-current.md) and [CSV](reports/time-zones-current.csv)

Reproduce the additional run:

```powershell
dotnet run -c Release --project benchmarks/Soenneker.Cron.Parser.Benchmarks -- --filter '*ParserBenchmarks*' '*ZonedBenchmarks*' --iterationCount 8 --artifacts benchmarks/results/focused
```
## Earlier focused runs

These historical ShortRun reports use three warmup and three measurement iterations
and earlier revisions. They are separate evidence, not rows in the current comparison:

- [Initial UTC and special-day report](reports/parser.md)
- [Initial time-zone report](reports/time-zones.md)
- [Time-zone follow-up after the issue review](reports/time-zones-issue-review.md)

The follow-up measured ordinary, spring-gap, and fall-overlap searches at 122.8 ns,
546.7 ns, and 676.8 ns respectively for Soenneker, versus 142.9 ns, 649.1 ns, and
1,247.3 ns for Cronos. All three Soenneker cases measured 0 B. The overlap has a
broad confidence interval; do not infer a precise speedup from these short runs.
