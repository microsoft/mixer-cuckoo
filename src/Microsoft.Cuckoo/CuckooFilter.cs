// -----------------------------------------------------------------------
// <copyright file="CuckooFilter.cs" company="Microsoft">
//     Copyright (c) Microsoft. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Cuckoo.UnitTests")]

namespace Microsoft.Cuckoo
{
  using System;
  using System.Collections.Generic;
  using System.Text;

  /// <summary>
  /// Implementation of the Cuckoo filter.
  /// </summary>
  public class CuckooFilter : ICuckooFilter, IEquatable<CuckooFilter>
  {
    /// <summary>
    /// Number of bytes in a <see cref="uint"/>
    /// </summary>
    private const int uint32Bytes = sizeof(uint);

    /// <summary>
    /// Hashing algorithm.
    /// </summary>
    private readonly IHashAlgorithm hashAlgorithm;

    /// <summary>
    /// Random instance.
    /// </summary>
    internal readonly Random random;

    /// <summary>
    /// Pre-allocated buffer we hash value indices into.
    /// </summary>
    private readonly byte[] valuesBuffer = new byte[CuckooFilter.uint32Bytes];

    /// <summary>
    /// Pre-allocated buffer we hash the fingerprint into.
    /// </summary>
    private byte[] fingerprintBuffer;

    /// <summary>
    /// Pre-allocated buffer we use for swapping fingerprints into.
    /// </summary>
    private byte[] fingerprintSwapBuffer;

    /// <summary>
    /// Comparator function, see <see cref="CodegenHelpers.CreateFingerprintComparator"/>
    /// </summary>
    private readonly Func<byte[], int, byte[], int> comparator;

    /// <summary>
    /// Comparator function, see <see cref="CodegenHelpers.CreateZeroChecker"/>
    /// </summary>
    private readonly Func<byte[], int, bool> zeroCheck;

    /// <summary>
    /// Comparator function, see <see cref="CodegenHelpers.CreateInsertIntoBucket"/>
    /// </summary>
    private readonly Func<byte[], int, byte[], bool> InsertIntoBucket;

    /// <summary>
    /// Creates a new CuckooFilter.
    /// </summary>
    /// <param name="buckets">Number of Buckets to store</param>
    /// <param name="entriesPerBucket">Number of fingerprints stored
    /// in each bucket.</param>
    /// <param name="fingerprintLength">Length of the fingerprint to use</param>
    /// <param name="maxKicks">Maximum number of times to relocate a value
    /// on a collision.</param>
    /// <param name="hashAlgorithm">Hashing algorithm to use</param>
    /// <param name="randomSeed">Random seed value</param>
    public CuckooFilter(
        uint buckets,
        uint entriesPerBucket,
        uint fingerprintLength,
        uint? maxKicks = null,
        IHashAlgorithm hashAlgorithm = null,
        int? randomSeed = null)
    {
      this.Buckets = buckets;
      this.EntriesPerBucket = entriesPerBucket;
      this.hashAlgorithm = hashAlgorithm ?? XxHashAlgorithm.Instance;
      this.MaxKicks = maxKicks ?? buckets;

      if (CuckooFilter.UpperPower2(buckets) != buckets)
      {
        throw new ArgumentException("Buckets must be a power of 2", nameof(buckets));
      }

      this.fingerprintBuffer = new byte[fingerprintLength];
      this.Contents = CuckooFilter.CreateEmptyBucketData(buckets, entriesPerBucket, fingerprintLength);
      this.random = randomSeed == null
          ? new Random()
          : new Random(randomSeed.Value);
      this.BytesPerBucket = this.EntriesPerBucket * fingerprintLength;
      this.fingerprintSwapBuffer = new byte[this.fingerprintBuffer.Length];
      this.comparator = CodegenHelpers.CreateFingerprintComparator(fingerprintLength, entriesPerBucket);
      this.zeroCheck = CodegenHelpers.CreateZeroChecker(fingerprintLength);
      this.InsertIntoBucket = CodegenHelpers.CreateInsertIntoBucket(fingerprintLength, entriesPerBucket);
    }

    /// <summary>
    /// Creates a new CuckooFilter.
    /// </summary>
    /// <param name="contents">Contents of th efilter</param>
    /// <param name="entriesPerBucket">Number of fingerprints stored
    /// in each bucket.</param>
    /// <param name="fingerprintLength">Length of the fingerprint to use</param>
    /// <param name="maxKicks">Maximum number of times to relocate a value
    /// on a collision.</param>
    /// <param name="hashAlgorithm">Hashing algorithm to use</param>
    internal CuckooFilter(
        byte[] contents,
        uint entriesPerBucket,
        uint fingerprintLength,
        uint maxKicks,
        IHashAlgorithm hashAlgorithm = null)
    {
      this.Contents = contents;
      this.MaxKicks = maxKicks;
      this.EntriesPerBucket = entriesPerBucket;
      this.BytesPerBucket = entriesPerBucket * fingerprintLength;
      this.Buckets = (uint)contents.Length / this.BytesPerBucket;
      this.fingerprintBuffer = new byte[fingerprintLength];
      this.fingerprintSwapBuffer = new byte[this.fingerprintBuffer.Length];
      this.hashAlgorithm = hashAlgorithm ?? XxHashAlgorithm.Instance;
      this.zeroCheck = CodegenHelpers.CreateZeroChecker(fingerprintLength);
      this.comparator = CodegenHelpers.CreateFingerprintComparator(fingerprintLength, 4);
      this.InsertIntoBucket = CodegenHelpers.CreateInsertIntoBucket(fingerprintLength, entriesPerBucket);
    }

    /// <summary>
    /// Creates a new optimally-sized CuckooFilter with a target
    /// false-positive-at-capacity.
    /// </summary>
    /// <param name="capacity">Filter capacity</param>
    /// <param name="falsePositiveRate">Desired false positive rate.</param>
    /// <param name="hashAlgorithm">Hashing algorithm to use</param>
    /// <param name="randomSeed">Random seed value</param>
    public CuckooFilter(
        uint capacity,
        double falsePositiveRate,
        IHashAlgorithm hashAlgorithm = null,
        int? randomSeed = null)
    {
      this.hashAlgorithm = hashAlgorithm ?? XxHashAlgorithm.Instance;

      // "In summary, we choose (2, 4)-cuckoo filter (i.e., each item has
      // two candidate Buckets and each bucket has up to four fingerprints)
      // as the default configuration, because it achieves the best or
      // close - to - best space efficiency for the false positive
      // rates that most practical applications""
      this.EntriesPerBucket = 4;

      // Equation here from page 8, step 6, of the paper:
      // ceil(log_2 (2b / \epsilon)
      var desiredLength = Math.Log(2 * (float)this.EntriesPerBucket / falsePositiveRate, 2);
      var fingerprintLength = (uint)Math.Ceiling(desiredLength / 8);
      this.fingerprintBuffer = new byte[fingerprintLength];

      // Not explicitly defined in the paper, however this is the
      // algorithm used in the author's implementation:
      // https://github.com/efficient/cuckoofilter/blob/master/src/cuckoofilter.h#L89
      this.Buckets = CuckooFilter.UpperPower2(capacity / this.EntriesPerBucket);
      if ((double)capacity / this.Buckets / this.EntriesPerBucket > 0.96)
      {
        this.Buckets <<= 1;
      }

      this.MaxKicks = this.Buckets;
      this.random = randomSeed == null
          ? new Random()
          : new Random(randomSeed.Value);
      this.Contents = CuckooFilter.CreateEmptyBucketData(this.Buckets, this.EntriesPerBucket, fingerprintLength);
      this.BytesPerBucket = this.EntriesPerBucket * fingerprintLength;
      this.fingerprintSwapBuffer = new byte[this.fingerprintBuffer.Length];
      this.zeroCheck = CodegenHelpers.CreateZeroChecker(fingerprintLength);
      this.comparator = CodegenHelpers.CreateFingerprintComparator(fingerprintLength, 4);
      this.InsertIntoBucket = CodegenHelpers.CreateInsertIntoBucket(fingerprintLength, this.EntriesPerBucket);
    }

    /// <summary>
    /// Gets the number of Buckets the filter contains.
    /// </summary>
    public uint Buckets { get; }

    /// <summary>
    /// Number of bytes each bucket takes.
    /// </summary>
    public uint BytesPerBucket { get; }

    /// <summary>
    /// Gets the number of fingerprints to store per bucket.
    /// </summary>
    public uint EntriesPerBucket { get; }

    /// <summary>
    /// Gets the length of the fingerprint.
    /// </summary>
    public uint FingerprintLength
    {
      get
      {
        return (uint)this.fingerprintBuffer.Length;
      }
    }

    /// <summary>
    /// Gets the max number of times we'll try to kick and item from a
    /// bucket when we insert before giving it.
    /// </summary>
    public uint MaxKicks { get; }

    /// <summary>
    /// Gets the total size, in memory, of the filter.
    /// </summary>
    public uint Size
    {
      get
      {
        return (uint)this.Contents.Length;
      }
    }

    /// <summary>
    /// Contents of the cuckoo filter.
    /// </summary>
    internal byte[] Contents { get; }

    /// <summary>
    /// Returns a value indicating whether the filter probably contains
    /// the given item.
    /// </summary>
    /// <param name="value">Value to check</param>
    /// <returns>True if the filter contains the value, false otherwise.</returns>
    public bool Contains(byte[] value)
    {
      var fingerprint = this.GetFingerprint(value);
      this.hashAlgorithm.Hash(this.valuesBuffer, value, CuckooFilter.uint32Bytes);

      var index1 = CuckooFilter.ToInt32(this.valuesBuffer) & (this.Buckets - 1);
      if (this.comparator(this.Contents, this.IndexToOffset(index1), fingerprint) != -1)
      {
        return true;
      }

      var index2 = this.DeriveIndex2(fingerprint, index1);
      if (this.comparator(this.Contents, this.IndexToOffset(index2), fingerprint) != -1)
      {
        return true;
      }

      return false;
    }

    /// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>true if the current object is equal to the <paramref name="other">other</paramref> parameter; otherwise, false.</returns>
    public bool Equals(CuckooFilter other)
    {
      if (object.ReferenceEquals(null, other))
      {
        return false;
      }

      if (object.ReferenceEquals(this, other))
      {
        return true;
      }

      return this.Buckets == other.Buckets
             && this.BytesPerBucket == other.BytesPerBucket
             && CodegenHelpers.BytesEquals(this.Contents, other.Contents)
             && this.EntriesPerBucket == other.EntriesPerBucket
             && this.MaxKicks == other.MaxKicks;
    }

    /// <summary>Determines whether the specified object is equal to the current object.</summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
      if (object.ReferenceEquals(null, obj))
      {
        return false;
      }

      if (object.ReferenceEquals(this, obj))
      {
        return true;
      }

      if (obj.GetType() != this.GetType())
      {
        return false;
      }

      return Equals((CuckooFilter)obj);
    }

    /// <summary>Serves as the default hash function.</summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
      unchecked
      {
        var contentHash = new byte[4];
        this.hashAlgorithm.Hash(contentHash, this.Contents, 4);

        var hashCode = (int)this.Buckets;
        hashCode = (hashCode * 397) ^ (int)this.BytesPerBucket;
        hashCode = (hashCode * 397) ^ (contentHash[0] | (contentHash[1] << 8)
                                                      | (contentHash[2] << 16)
                                                      | (contentHash[3] << 24));
        hashCode = (hashCode * 397) ^ (int)this.EntriesPerBucket;
        hashCode = (hashCode * 397) ^ (int)this.MaxKicks;
        return hashCode;
      }
    }

    /// <summary>
    /// Inserts the value into the filter. Whereas <see cref="TryInsert"/>
    /// returns false if the value cannot be inserted, this throws.
    /// </summary>
    /// <param name="value">Value to insert</param>
    /// <exception cref="FilterFullException">Thrown if the filter is
    /// too full to accept the value.</exception>
    public void Insert(byte[] value)
    {
      if (!this.TryInsert(value))
      {
        throw new FilterFullException();
      }
    }

    /// <summary>
    /// Removes a value from the filter.
    /// </summary>
    /// <param name="value">Value to remove</param>
    /// <returns>True if the filter contained the value, false otherwise.</returns>
    public bool Remove(byte[] value)
    {
      var fingerprint = this.GetFingerprint(value);
      this.hashAlgorithm.Hash(this.valuesBuffer, value, CuckooFilter.uint32Bytes);

      var index1 = CuckooFilter.ToInt32(this.valuesBuffer) & (this.Buckets - 1);

      var offset = this.IndexToOffset(index1);
      var removal = this.comparator(this.Contents, offset, fingerprint);
      if (removal != -1)
      {
        Array.Clear(this.Contents, offset + this.fingerprintBuffer.Length * removal, fingerprint.Length);
        return true;
      }


      var index2 = this.DeriveIndex2(fingerprint, index1);
      offset = this.IndexToOffset(index2);
      removal = this.comparator(this.Contents, offset, fingerprint);
      if (removal != -1)
      {
        Array.Clear(this.Contents, offset + this.fingerprintBuffer.Length * removal, fingerprint.Length);
        return true;
      }

      return false;
    }

    /// <summary>
    /// Attempts to insert the value into the filter.
    /// </summary>
    /// <param name="value">Value to insert</param>
    /// <returns>True if it was inserted successfully, false if the
    /// filter was too full to do so.</returns>
    public bool TryInsert(byte[] value)
    {
      return this.TryInsertInner(value);
    }

    /// <summary>
    /// Attempts to insert the value into the filter.
    /// </summary>
    /// <param name="value">Value to insert</param>
    /// <returns>True if it was inserted successfully, false if the
    /// filter was too full to do so.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryInsertInner(byte[] value)
    {
      var fingerprint = this.GetFingerprint(value);
      this.hashAlgorithm.Hash(this.valuesBuffer, value, CuckooFilter.uint32Bytes);
      var index1 = BoundToBucketCount(CuckooFilter.ToInt32(this.valuesBuffer));

      if (this.InsertIntoBucket(this.Contents, IndexToOffset(index1), fingerprint))
      {
        return true;
      }

      var index2 = this.DeriveIndex2(fingerprint, index1);
      if (this.InsertIntoBucket(this.Contents, IndexToOffset(index2), fingerprint))
      {
        return true;
      }

      var targetIndex = this.random.Next(1) == 0
          ? index1
          : index2;

      for (var i = 0; i < this.MaxKicks; i++)
      {
        fingerprint = this.SwapIntoBucket(fingerprint, targetIndex);
        targetIndex = this.DeriveIndex2(fingerprint, targetIndex);

        if (this.InsertIntoBucket(this.Contents, IndexToOffset(targetIndex), fingerprint))
        {
          return true;
        }
      }

      return false;
    }

    /// <summary>
    /// Returns a nicely formatted version of the filter. Used for
    /// examining internal state while testing.
    /// </summary>
    /// <returns></returns>
    internal IList<IList<string>> DumpDebug()
    {
      var list = new List<IList<string>>();
      for (var offset = 0L; offset < this.Contents.Length; offset += this.BytesPerBucket)
      {
        var items = new List<string>();
        for (var i = 0; i < this.EntriesPerBucket; i++)
        {
          var target = (int)offset + i * this.fingerprintBuffer.Length;
          if (!this.zeroCheck(this.Contents, target))
          {
            items.Add(Encoding.ASCII.GetString(this.Contents, target, this.fingerprintBuffer.Length));
          }
        }

        list.Add(items);
      }

      return list;
    }

    private static byte[] CreateEmptyBucketData(long buckets, uint itemsPerBucket, uint bytesPerItem)
    {
      return new byte[buckets * itemsPerBucket * bytesPerItem];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ToInt32(byte[] data)
    {
      int x = data[0] << 24;
      x |= data[1] << 16;
      x |= data[2] << 8;
      x |= data[3];
      return x;
    }

    private static uint UpperPower2(uint x)
    {
      x--;
      x |= x >> 1;
      x |= x >> 2;
      x |= x >> 4;
      x |= x >> 8;
      x |= x >> 16;
      x |= x >> 32;
      x++;
      return x;
    }

    /// <summary>
    /// Ensures the value is less than or equal to the number of Buckets.
    /// </summary>
    /// <param name="value">Value to bound</param>
    /// <returns>Truncated value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long BoundToBucketCount(long value)
    {
      // this.Buckets is always a power of 2, so to ensure index1 is <=1
      // the number of Buckets, we mask it against Buckets - 1. So if
      // Buckets is 16 (0b10000), we mask it against (0b01111).
      return value & (this.Buckets - 1);
    }

    /// <summary>
    /// Get the alternative index for an item, given its primary
    /// index and fingerprint.
    /// </summary>
    /// <param name="fingerprint">Fingerprint value</param>
    /// <param name="index1">Primary index</param>
    /// <returns>The secondary index</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long DeriveIndex2(byte[] fingerprint, long index1)
    {
      this.hashAlgorithm.Hash(this.valuesBuffer, fingerprint, CuckooFilter.uint32Bytes);
      return index1 ^ this.BoundToBucketCount(CuckooFilter.ToInt32(this.valuesBuffer));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte[] GetFingerprint(byte[] input)
    {
      this.hashAlgorithm.Hash(this.fingerprintBuffer, input, this.fingerprintBuffer.Length);
      if (this.zeroCheck(this.fingerprintBuffer, 0))
      {
        for (var i = 0; i < this.fingerprintBuffer.Length; i++)
        {
          this.fingerprintBuffer[i] = 0xff;
        }
      }

      return this.fingerprintBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IndexToOffset(long index)
    {
      return (int)(index * this.BytesPerBucket);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte[] SwapIntoBucket(byte[] fingerprint, long bucket)
    {
      var subIndex = this.random.Next((int)this.EntriesPerBucket);
      var offset = this.IndexToOffset(bucket) + subIndex * fingerprint.Length;
      var newFingerprint = this.fingerprintSwapBuffer;

      Array.Copy(this.Contents, offset, this.fingerprintSwapBuffer, 0, fingerprint.Length);
      Array.Copy(fingerprint, 0, this.Contents, offset, fingerprint.Length);
      this.fingerprintSwapBuffer = fingerprint;
      this.fingerprintBuffer = newFingerprint;

      return newFingerprint;
    }
  }
}
