// -----------------------------------------------------------------------
// <copyright file="PrefixHashAlgorithm.cs" company="Microsoft">
//     Copyright (c) Microsoft. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Cuckoo.UnitTests
{
  /// <summary>
  /// A hash algorithm which just "hashes" the first few bytes of the string,
  /// for use in testing.
  /// </summary>
  internal class PrefixHashAlgorithm : IHashAlgorithm
  {
    private readonly int lengthLimit;

    /// <summary>
    /// Creates a new test algorithm.
    /// </summary>
    /// <param name="lengthLimit">Prefix length to use</param>
    public PrefixHashAlgorithm(int lengthLimit = int.MaxValue)
    {
      this.lengthLimit = lengthLimit;
    }

    /// <summary>
    /// Returns a hash of the value.
    /// </summary>
    /// <param name="target">Target array to hash to</param>
    /// <param name="value">Value to hash</param>
    /// <param name="hashLength">Number of bytes to write into the target.</param>
    public void Hash(byte[] target, byte[] value, int hashLength)
    {
      for (var i = 0; i < hashLength && i < this.lengthLimit && i < value.Length; i++)
      {
        target[i] = value[i];
      }
    }
  }
}
