// -----------------------------------------------------------------------
// <copyright file="MappedHashAlgorithm.cs" company="Microsoft">
//     Copyright (c) Microsoft. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Cuckoo.UnitTests
{
  using System;
  using System.Collections.Generic;
  using System.Text;

  /// <summary>
  /// A hash algorithm which has an explicit map of keys to hash values,
  /// for use in testing.
  /// </summary>
  internal class MappedHashAlgorithm : IHashAlgorithm
  {
    private readonly IDictionary<string, string> mapping;

    /// <summary>
    /// Creates a new test algorithm.
    /// </summary>
    /// <param name="mapping">Mapping of keys to hashed values</param>
    public MappedHashAlgorithm(IDictionary<string, string> mapping)
    {
      this.mapping = mapping;
    }

    /// <summary>
    /// Returns a hash of the value.
    /// </summary>
    /// <param name="target">Target array to hash to</param>
    /// <param name="value">Value to hash</param>
    /// <param name="hashLength">Number of bytes to write into the target.</param>
    public void Hash(byte[] target, byte[] value, int hashLength)
    {
      var str = Encoding.ASCII.GetString(value);
      if (!this.mapping.TryGetValue(str, out var hashed))
      {
        throw new InvalidOperationException($"Asked to hash unknown value {str}");
      }

      Array.Copy(Encoding.ASCII.GetBytes(hashed), target, Math.Min(hashLength, hashed.Length));
    }
  }
}
