// -----------------------------------------------------------------------
// <copyright file="CodegenHelpers.cs" company="Microsoft">
//     Copyright (c) Microsoft. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Cuckoo.UnitTests
{
  using System.Linq;
  using Microsoft.VisualStudio.TestTools.UnitTesting;
  using Shouldly;

  [TestClass]
  public class CodeGenTests
  {
    [TestMethod]
    public void TestComparator()
    {
      var cmp = CodegenHelpers.CreateFingerprintComparator(2, 4);
      var a = Enumerable.Range(1, 50).Select(v => (byte)v).ToArray();
      var b = new byte[] { 9, 10 };

      cmp(a, 0, b).ShouldBe(-1);
      cmp(a, 1, b).ShouldBe(-1);
      cmp(a, 2, b).ShouldBe(3);
      cmp(a, 3, b).ShouldBe(-1);
      cmp(a, 4, b).ShouldBe(2);
      cmp(a, 5, b).ShouldBe(-1);
      cmp(a, 6, b).ShouldBe(1);
      cmp(a, 7, b).ShouldBe(-1);
      cmp(a, 8, b).ShouldBe(0);
      cmp(a, 9, b).ShouldBe(-1);
      cmp(a, 10, b).ShouldBe(-1);
    }

    [TestMethod]
    public void TestZero()
    {
      var check = CodegenHelpers.CreateZeroChecker(2);
      var a = new byte[] { 1, 2, 0, 0, 4, 5 };

      check(a, 0).ShouldBeFalse();
      check(a, 1).ShouldBeFalse();
      check(a, 2).ShouldBeTrue();
      check(a, 3).ShouldBeFalse();
      check(a, 4).ShouldBeFalse();
    }


    [TestMethod]
    public void TestSwapIn()
    {
      var swap = CodegenHelpers.CreateInsertIntoBucket(2, 2);
      var a = new byte[] { 1, 2, 0, 0, 4, 5, 6, 7 };

      swap(a, 0, new byte[] { 8, 9 }).ShouldBeTrue();
      string.Join(" ", a.Select(v => v.ToString())).ShouldBe("1 2 8 9 4 5 6 7");

      swap(a, 0, new byte[] { 8, 9 }).ShouldBeFalse();
      swap(a, 4, new byte[] { 8, 9 }).ShouldBeFalse();

      string.Join(" ", a.Select(v => v.ToString())).ShouldBe("1 2 8 9 4 5 6 7");
    }
  }
}
