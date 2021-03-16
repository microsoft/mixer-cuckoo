// -----------------------------------------------------------------------
// <copyright file="PDSBenchmark.cs" company="Microsoft">
//     Copyright (c) Microsoft. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Cuckoo.Benchmark
{
  using System;
  using BenchmarkDotNet.Attributes;
  using BenchmarkDotNet.Jobs;
  using Cuckoo.Benchmark.PDS;
  using CuckooFilter = Cuckoo.CuckooFilter;

  [RankColumn]
  [SimpleJob(RuntimeMoniker.NetCoreApp50)]
  public class PDSBenchmark
  {
    private readonly int Capacity = 10_000;
    private readonly Random random = new Random();
    private ICuckooFilter mixerFilter;

    [Params(true, false)]
    public bool PDS;

    private CuckooBloomFilter pdsFilter;

    [Benchmark]
    public bool Contains()
    {
      var value = BitConverter.GetBytes(this.random.Next(this.Capacity));
      return this.PDS
          ? this.pdsFilter.Test(value)
          : this.mixerFilter.Contains(value);
    }

    [Benchmark]
    public void Insert()
    {
      var value = BitConverter.GetBytes(this.random.Next(this.Capacity));
      if (this.PDS)
      {
        this.pdsFilter.Add(value);
        this.pdsFilter.TestAndRemove(value);
      }
      else
      {
        this.mixerFilter.Insert(value);
        this.mixerFilter.Remove(value);
      }
    }

    [GlobalSetup]
    public void Setup()
    {
      this.mixerFilter = new CuckooFilter((uint)this.Capacity, 0.01, randomSeed: 0);
      this.pdsFilter = new CuckooBloomFilter((uint)this.Capacity, 0.01);

      for (var i = 0; i < this.Capacity / 2; i++)
      {
        this.mixerFilter.Insert(BitConverter.GetBytes(this.random.Next(this.Capacity)));
      }
    }
  }
}
