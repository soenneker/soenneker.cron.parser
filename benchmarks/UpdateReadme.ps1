param(
    [string]$ReportPath = (Join-Path $PSScriptRoot 'reports/comparison.csv')
)

$ErrorActionPreference = 'Stop'
$rows = @(Import-Csv -LiteralPath $ReportPath)
$scenarios = [ordered]@{
    EveryMinute = 'Every minute'
    Every15Seconds = 'Every 15 seconds'
    Weekdays = 'Weekdays at 09:00'
    LeapDay = 'Leap day'
    Lists = 'Numeric lists'
    Names = 'Names and ranges'
}
$libraries = [ordered]@{
    Soenneker = 'Soenneker'
    Cronos = 'Cronos'
    NCrontab = 'NCrontab'
    Quartz = 'Quartz.NET'
    CronParser = 'CronParser'
}

function Get-Nanoseconds([string]$mean) {
    if ($mean -notmatch '^(?<value>[\d,.]+)\s+(?<unit>ns|us|µs|μs|ms|s)$') {
        throw "Unrecognized benchmark mean: $mean"
    }
    $value = [double]::Parse($Matches.value, [Globalization.CultureInfo]::InvariantCulture)
    switch ($Matches.unit) {
        ns { return $value }
        ms { return $value * 1000000 }
        s  { return $value * 1000000000 }
        default { return $value * 1000 }
    }
}

$lines = [Collections.Generic.List[string]]::new()
$lines.Add('BenchmarkDotNet 0.15.8 · .NET 10.0.11.')
$lines.Add('Measured September 5, 2026 (local time), with 3 warmup and 8 measurement iterations per case.')
$lines.Add('')
$lines.Add('Versions: **Cronos 0.13.0**, **NCrontab 3.4.0**, **Quartz 4.0.0**, **CronParser 1.3.0**.')
$lines.Add('Each cell shows **time / allocated bytes per operation**.')

foreach ($category in @('Next', 'Parse')) {
    $lines.Add('')
    $lines.Add($(if ($category -eq 'Next') { '### Next occurrence' } else { '### Parse a new schedule' }))
    $lines.Add('')
    $lines.Add('| Schedule | ' + ($libraries.Values -join ' | ') + ' |')
    $lines.Add('|---|---:|---:|---:|---:|---:|')
    foreach ($scenario in $scenarios.Keys) {
        $results = @()
        foreach ($library in $libraries.Keys) {
            $matching = @($rows | Where-Object { $_.Categories -eq $category -and $_.Scenario -eq $scenario -and $_.Method -eq "$library$category" })
            if ($matching.Count -ne 1) { throw "Expected one result for $library / $category / $scenario; found $($matching.Count)" }
            $results += $matching[0]
        }
        $fastest = ($results | ForEach-Object { Get-Nanoseconds $_.Mean } | Measure-Object -Minimum).Minimum
        $cells = foreach ($result in $results) {
            $time = $result.Mean
            if ((Get-Nanoseconds $time) -eq $fastest) { $time = "**$time**" }
            $allocation = if ($result.Allocated -eq '-') { '0 B' } else { $result.Allocated }
            "$time / $allocation"
        }
        $lines.Add('| ' + $scenarios[$scenario] + ' | ' + ($cells -join ' | ') + ' |')
    }
}

$lines.Add('')
$lines.Add('[Full BenchmarkDotNet output, errors, and standard deviations](benchmarks/reports/comparison.md).')
$readmePath = Join-Path $PSScriptRoot '../README.md'
$readme = [IO.File]::ReadAllText($readmePath)
$startMarker = '<!-- BEGIN BENCHMARKS -->'
$endMarker = '<!-- END BENCHMARKS -->'
$start = $readme.IndexOf($startMarker)
$end = $readme.IndexOf($endMarker)
if ($start -lt 0 -or $end -lt $start) { throw 'Benchmark markers are missing from README.md' }
$updated = $readme.Substring(0, $start + $startMarker.Length) + "`n`n" + ($lines -join "`n") + "`n`n" + $readme.Substring($end)
[IO.File]::WriteAllText($readmePath, $updated.TrimEnd() + "`n")
Write-Output 'Updated README.md from the complete benchmark CSV.'
