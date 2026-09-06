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
| **CronosNext**    | **FallOverlap** | **1,247.3 ns** |  **38.85 ns** |  **2.13 ns** |  **1.00** |    **0.00** | **0.0019** |      **40 B** |        **1.00** |
| SoennekerNext | FallOverlap |   676.8 ns | 290.89 ns | 15.94 ns |  0.54 |    0.01 |      - |         - |        0.00 |
|               |             |            |           |          |       |         |        |           |             |
| **CronosNext**    | **Ordinary**    |   **142.9 ns** |  **49.04 ns** |  **2.69 ns** |  **1.00** |    **0.02** |      **-** |         **-** |          **NA** |
| SoennekerNext | Ordinary    |   122.8 ns |  14.08 ns |  0.77 ns |  0.86 |    0.01 |      - |         - |          NA |
|               |             |            |           |          |       |         |        |           |             |
| **CronosNext**    | **SpringGap**   |   **649.1 ns** |  **16.82 ns** |  **0.92 ns** |  **1.00** |    **0.00** |      **-** |         **-** |          **NA** |
| SoennekerNext | SpringGap   |   546.7 ns |  43.98 ns |  2.41 ns |  0.84 |    0.00 |      - |         - |          NA |
