```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
AMD Ryzen Threadripper PRO 9995WX 96-Cores 2.50GHz, 1 CPU, 192 logical and 96 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=8  LaunchCount=1
WarmupCount=3

```
| Method        | Scenario    | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------- |------------ |-----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| **CronosNext**    | **FallOverlap** | **1,303.7 ns** | **33.18 ns** | **17.35 ns** |  **1.00** |    **0.02** | **0.0019** |      **40 B** |        **1.00** |
| SoennekerNext | FallOverlap |   624.1 ns | 13.48 ns |  7.05 ns |  0.48 |    0.01 |      - |         - |        0.00 |
|               |             |            |          |          |       |         |        |           |             |
| **CronosNext**    | **Ordinary**    |   **145.2 ns** |  **2.94 ns** |  **1.30 ns** |  **1.00** |    **0.01** |      **-** |         **-** |          **NA** |
| SoennekerNext | Ordinary    |   127.8 ns |  2.52 ns |  1.32 ns |  0.88 |    0.01 |      - |         - |          NA |
|               |             |            |          |          |       |         |        |           |             |
| **CronosNext**    | **SpringGap**   |   **649.4 ns** | **10.50 ns** |  **5.49 ns** |  **1.00** |    **0.01** |      **-** |         **-** |          **NA** |
| SoennekerNext | SpringGap   |   598.0 ns | 22.72 ns | 10.09 ns |  0.92 |    0.02 |      - |         - |          NA |
