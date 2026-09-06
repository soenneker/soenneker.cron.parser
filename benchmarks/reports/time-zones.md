```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
AMD Ryzen Threadripper PRO 9995WX 96-Cores 2.50GHz, 1 CPU, 192 logical and 96 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method        | Scenario    | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------- |------------ |-----------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| **CronosNext**    | **FallOverlap** | **1,287.9 ns** | **427.54 ns** | **23.44 ns** |  **1.00** |    **0.02** | **0.0019** |      **40 B** |        **1.00** |
| SoennekerNext | FallOverlap |   603.6 ns |  71.89 ns |  3.94 ns |  0.47 |    0.01 |      - |         - |        0.00 |
|               |             |            |           |          |       |         |        |           |             |
| **CronosNext**    | **Ordinary**    |   **142.0 ns** |  **15.12 ns** |  **0.83 ns** |  **1.00** |    **0.01** |      **-** |         **-** |          **NA** |
| SoennekerNext | Ordinary    |   124.8 ns |   8.61 ns |  0.47 ns |  0.88 |    0.01 |      - |         - |          NA |
|               |             |            |           |          |       |         |        |           |             |
| **CronosNext**    | **SpringGap**   |   **629.2 ns** |   **1.56 ns** |  **0.09 ns** |  **1.00** |    **0.00** |      **-** |         **-** |          **NA** |
| SoennekerNext | SpringGap   |   571.0 ns | 149.58 ns |  8.20 ns |  0.91 |    0.01 |      - |         - |          NA |
