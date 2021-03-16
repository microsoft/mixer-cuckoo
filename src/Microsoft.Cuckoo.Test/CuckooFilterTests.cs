// -----------------------------------------------------------------------
// <copyright file="CuckooFilterTests.cs" company="Microsoft">
//     Copyright (c) Microsoft. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Cuckoo.UnitTests
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Microsoft.VisualStudio.TestTools.UnitTesting;
  using Newtonsoft.Json;
  using Shouldly;

  [TestClass]
  public class CuckooFilterTests
  {
    [TestMethod]
    public void TestCascadesKicks()
    {
      // Here's we make four Buckets and insert items with different
      // hashes to observe the cascade. In order:
      // - The first three items insert into Buckets 1-3
      // - The forth item will try to insert into either bucket 1 or 2.
      //   It kicks the item in bucket 2 out, which falls into bucket 0.
      // - Subsequent insertions fail.
      var filter = new CuckooFilter(
          buckets: 4,
          entriesPerBucket: 1,
          fingerprintLength: 4,
          randomSeed: 0,
          hashAlgorithm: new MappedHashAlgorithm(
              new Dictionary<string, string>()
              {
                        { "foo1", "has1" },
                        { "foo2", "has2" },
                        { "foo3", "has3" },
                        { "foo4", "2as2" },
                        { "has1", "alt1" },
                        { "has2", "alt2" },
                        { "has3", "alt3" },
                        { "2as2", "alt1" },
              }));

      filter.InsertString("foo1").ShouldBeTrue();
      filter.ContainsString("foo1").ShouldBeTrue();
      this.ExpectState(filter, new[] { "", "has1", "", "" });

      filter.InsertString("foo2").ShouldBeTrue();
      filter.ContainsString("foo1").ShouldBeTrue();
      filter.ContainsString("foo2").ShouldBeTrue();
      this.ExpectState(filter, new[] { "", "has1", "has2", "" });

      filter.TryInsertString("foo3").ShouldBeTrue();
      this.ExpectState(filter, new[] { "", "has1", "has2", "has3" });

      filter.TryInsertString("foo4").ShouldBeTrue();
      this.ExpectState(filter, new[] { "has2", "has1", "2as2", "has3" });

      filter.TryInsertString("foo4").ShouldBeFalse();
    }

    [TestMethod]
    public void TestExpectedFalsePositiveProbabilitiesAreMet()
    {
      var expected = 0.03;
      var queryItems = 10_000;
      var maxFalsePositives = (int)(queryItems * expected);
      var itemCounts = new[] { 100, 1000, 10_000, 100_000, 1_000_000 };

      foreach (var itemCount in itemCounts)
      {
        var filter = new CuckooFilter((uint)itemCount, expected, randomSeed: 0);
        for (var i = 0; i < itemCount; i++)
        {
          filter.Insert(BitConverter.GetBytes(i));
        }

        for (var i = 0; i < itemCount; i++)
        {
          var contains = filter.Contains(BitConverter.GetBytes(i));
          contains.ShouldBeTrue();
        }

        var falsePositives = 0;
        for (var i = 0; i < queryItems; i++)
        {
          if (filter.Contains(BitConverter.GetBytes(i + itemCount)))
          {
            falsePositives++;
          }
        }

        falsePositives.ShouldBeLessThan(maxFalsePositives);
      }
    }

    [TestMethod]
    public void TestFailedOnNoMoreKicks()
    {
      // Here's we make four Buckets with only one entry per bucket, and
      // then try to insert 3 items with the same hash. In order:
      // - The first item inserts into bucket 0
      // - The second item inserts into bucket 3 (seeing that A is occupied)
      // - The third item tries to kick the first or second items, but
      //   both their indexes are full--this will fail.
      var filter = new CuckooFilter(
          buckets: 4,
          entriesPerBucket: 1,
          fingerprintLength: 4,
          hashAlgorithm: new MappedHashAlgorithm(
              new Dictionary<string, string>()
              {
                        { "foo1", "hash" },
                        { "foo2", "hash" },
                        { "foo3", "hash" },
                        { "hash", "altk" },
              }));

      filter.InsertString("foo1").ShouldBeTrue();
      filter.ContainsString("foo1").ShouldBeTrue();
      this.ExpectState(filter, new[] { "hash", "", "", "" });

      filter.InsertString("foo2").ShouldBeTrue();
      filter.ContainsString("foo1").ShouldBeTrue();
      filter.ContainsString("foo2").ShouldBeTrue();
      this.ExpectState(filter, new[] { "hash", "", "", "hash" });

      filter.TryInsertString("foo3").ShouldBeFalse(); // saturated
    }

    [TestMethod]
    public void TestInsertsMultipleSets()
    {
      // Here's we make four Buckets and insert items with different
      // hashes to observe the cascade. In order:
      // - The first four items load up the first two Buckets.
      // - The forth item will try to insert into the second bucket,
      //   which will kick foo4 out to the forth bucket.
      // - The fifth item will kick foo5 out, which bounces into the
      //   first bucket and kicks foo1 out to the third bucket.
      var filter = new CuckooFilter(
          buckets: 4,
          entriesPerBucket: 2,
          fingerprintLength: 4,
          randomSeed: 0,
          hashAlgorithm: new MappedHashAlgorithm(
              new Dictionary<string, string>()
              {
                        { "foo1", "aaa0" },
                        { "foo2", "bbb0" },

                        { "foo3", "aaa1" },
                        { "foo4", "bbb1" },

                        { "foo5", "ccc1" },
                        { "foo6", "ddd1" },
                        { "foo7", "eee1" },

                        { "aaa0", "alt2" },
                        { "ccc1", "alt1" },
                        { "ddd1", "alt1" },
                        { "bbb1", "alt2" },
              }));

      filter.InsertString("foo1").ShouldBeTrue();
      this.ExpectState(filter, new[] { "aaa0", "", "", "" });
      filter.InsertString("foo2").ShouldBeTrue();
      this.ExpectState(filter, new[] { "aaa0 bbb0", "", "", "" });

      filter.InsertString("foo3").ShouldBeTrue();
      this.ExpectState(filter, new[] { "aaa0 bbb0", "aaa1", "", "" });
      filter.InsertString("foo4").ShouldBeTrue();
      this.ExpectState(filter, new[] { "aaa0 bbb0", "aaa1 bbb1", "", "" });

      filter.InsertString("foo5").ShouldBeTrue();
      this.ExpectState(filter, new[] { "aaa0 bbb0", "aaa1 ccc1", "", "bbb1" });
      filter.InsertString("foo6").ShouldBeTrue();
      this.ExpectState(filter, new[] { "ccc1 bbb0", "aaa1 ddd1", "aaa0", "bbb1" });
    }

    [TestMethod]
    public void TestInsertsSimpleItem()
    {
      var filter = new CuckooFilter(100, 0.01);
      filter.ContainsString("foo").ShouldBeFalse();
      filter.InsertString("foo");
      filter.ContainsString("foo").ShouldBeTrue();
    }

    [TestMethod]
    public void TestSerializesBasic()
    {
      var filter = new CuckooFilter(100, 0.01);
      filter.ContainsString("foo").ShouldBeFalse();
      filter.InsertString("foo");
      filter.ContainsString("foo").ShouldBeTrue();
    }

    private void ExpectState(CuckooFilter filter, IList<string> expected)
    {
      var expectedStr = JsonConvert.SerializeObject(
          expected.Select(
              v => string.IsNullOrEmpty(v)
                  ? new string[0]
                  : v.Split(' ')),
          Formatting.Indented);
      var actualStr = JsonConvert.SerializeObject(filter.DumpDebug(), Formatting.Indented);
      actualStr.ShouldBe(expectedStr);
    }
  }
}
