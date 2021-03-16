// -----------------------------------------------------------------------
// <copyright file="SerializerTests.cs" company="Microsoft">
//     Copyright (c) Microsoft. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Cuckoo.UnitTests
{
  using System;
  using System.IO;
  using Microsoft.VisualStudio.TestTools.UnitTesting;
  using Shouldly;

  [TestClass]
  public class SerializerTests
  {
    [TestMethod]
    public void TestGzipSerializer()
    {
      this.RoundTrip(new GzipStreamSerializer(), this.MakeFilter());
    }

    [TestMethod]
    public void TestSimpleStreamSerializer()
    {
      this.RoundTrip(new SimpleStreamSerializer(), this.MakeFilter());
    }

    private CuckooFilter MakeFilter()
    {
      var filter = new CuckooFilter(500, 0.01, randomSeed: 50);
      for (var i = 0; i < 300; i++)
      {
        filter.Insert(BitConverter.GetBytes(i));
      }

      return filter;
    }

    private void RoundTrip(ICuckooSerializer serializer, CuckooFilter filter)
    {
      var buffer = new MemoryStream();
      serializer.Serialize(buffer, filter);
      Console.WriteLine($"len: {buffer.ToArray().Length}");
      buffer.Position = 0;
      var output = serializer.Deserialize(buffer);
      output.Equals(filter).ShouldBeTrue();
    }
  }
}
