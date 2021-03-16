// -----------------------------------------------------------------------
// <copyright file="CodegenHelpers.cs" company="Microsoft">
//     Copyright (c) Microsoft. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Cuckoo.Benchmark
{
  using BenchmarkDotNet.Running;

  class Program
  {
    static void Main(string[] args)
    {
      BenchmarkRunner.Run<CuckooFilterBenchmarks>();
      BenchmarkRunner.Run<PDSBenchmark>();
    }
  }
}
