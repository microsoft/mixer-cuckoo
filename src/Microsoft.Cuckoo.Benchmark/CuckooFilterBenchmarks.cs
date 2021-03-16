// -----------------------------------------------------------------------
// <copyright file="CuckooFilterBenchmarks.cs" company="Microsoft">
//     Copyright (c) Microsoft. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Cuckoo.Benchmark
{
  using System;
  using BenchmarkDotNet.Attributes;
  using BenchmarkDotNet.Jobs;

  [RankColumn]
  [SimpleJob(RuntimeMoniker.NetCoreApp50)]
  public class CuckooFilterBenchmarks
  {
    private readonly Random random = new Random();

    [Params(100, 1000, 10_000, 100_000, 1_000_000)]
    public int Capacity;

    [Params(0.03, 0.01, 0.001)]
    public double Precision;

    private ICuckooFilter filter;

    [Params(true)]
    public bool NewFilter;

    [Benchmark]
    public bool Contains()
    {
      return this.filter.Contains(BitConverter.GetBytes(this.random.Next(this.Capacity)));
    }

    [Benchmark]
    public void Insert()
    {
      var value = BitConverter.GetBytes(this.random.Next(this.Capacity));
      this.filter.Insert(value);
      this.filter.Remove(value);
    }

    [GlobalSetup]
    public void Setup()
    {
      this.filter = this.NewFilter
          ? (ICuckooFilter)new CuckooFilter((uint)this.Capacity, this.Precision, randomSeed: 0)
          : new PDS.CuckooFilter((uint)this.Capacity, this.Precision, randomSeed: 0);

      for (var i = 0; i < this.Capacity / 2; i++)
      {
        this.filter.Insert(BitConverter.GetBytes(this.random.Next(this.Capacity)));
      }
    }
  }
}
