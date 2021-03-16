# Cuckoo

A cuckoo filter implementation for C#. Cuckoo filters are probabilistic data structures which have the same use case as bloom filters, but generally works better and more efficiently.

References:

 - [Cuckoo Filter: Practically Better Than Bloom](https://www.cs.cmu.edu/~dga/papers/cuckoo-conext2014.pdf)
 - [Original C++ Research Implementation](https://github.com/efficient/cuckoofilter)
 - [An existing C# Cuckoo Filter Implementation](https://github.com/mattlorimor/ProbabilisticDataStructures/blob/master/ProbabilisticDataStructures/CuckooBloomFilter.cs)
 - [Another C# Cuckoo Filter Implementation](https://github.com/agustinsantos/CuckooFilter)

Notable features of this library--primarily over the other C# implementations--include:

 - Ability to efficient serialize and deserialize;
 - Zero-allocation (adding, testing, and removing elements causes no additional allocation);
 - Approximately 20-40x faster than existing implementations -- < 100ns for all operations;
 - Configurable hashing ([xxhash](https://cyan4973.github.io/xxHash/) by default);
 - Uses [Sigil](https://github.com/kevin-montrose/Sigil)/IL generation to increase speed by unrolling loops;
 - Published on NuGet! :)

### Usage

The filter has a pretty easy and intuitive API:

```csharp
// Create a filter that can hold 10k items with a ~1% false positive rate:
var filter = new CuckooFilter(10_000, 0.01);

// Insert an item into the filter:
filter.Insert(myItem);

// Note that the above will throw if the filter is full. You can alternately:
if (!filter.TryInsert(myItem))
{
    Console.WriteLine("Filter is full!");
}

// check that an item exists:
Console.WriteLine(filter.Contains(myItem)); // => true

// and remove items, returning whether it (might have) existed:
Console.WriteLine(filter.Remove(myItem)); // => true

// Serialize, somewhere...
var serializer = new GzipStreamSerializer();
serializer.Serialize(someStream, filter);

// And deserialize:
var anotherFilter = serializer.Deserialize(anotherStream);
```

### Performance

Benchmark.net performance results. In these tests, operations were run against filter after being half-loaded with data. `Contains` checked that a random item exists in the filter, and `Insert` inserts and deletes a random item from the filter.

In comparison to existing libraries, `PDS=true` indicates the test using the primary [existing cuckoo filter library]((https://github.com/mattlorimor/ProbabilisticDataStructures/blob/master/ProbabilisticDataStructures/CuckooBloomFilter.cs)), PSD=false is this one. "Insert" is both an insertion and deletion operation.

```
|   Method |   PDS |        Mean |      Error |     StdDev | Rank |
|--------- |------ |------------:|-----------:|-----------:|-----:|
| Contains | False |    61.02 ns |  0.3764 ns |  0.3336 ns |    1 |
|   Insert | False |    91.35 ns |  0.5319 ns |  0.4715 ns |    2 |
| Contains |  True | 1,371.08 ns | 12.5455 ns | 11.7351 ns |    3 |
|   Insert |  True | 2,952.71 ns | 29.0322 ns | 25.7363 ns |    4 |
```

You can run benchmarks by copying the PDS implementation into `src/Microsoft.Cuckoo.Benchmark/PDS`, and running `dotnet run --configuration Release --project .\src\Microsoft.Cuckoo.Benchmark\Microsoft.Cuckoo.Benchmark.csproj`.

Some more runtime information specific to this library, with various capacities and desired maximum false-positive rates. Once again, insertion runs both an insert and a delete. Runtime is nearly-linear, with increased runtimes at larger sizes primarily due to a loss in cache locality.

```
|   Method | Capacity | Precision | NewFilter |      Mean |     Error |    StdDev |    Median | Rank |
|--------- |--------- |---------- |---------- |----------:|----------:|----------:|----------:|-----:|
|   Insert |      100 |      0.01 |      True |  91.66 ns | 0.4769 ns | 0.3982 ns |  91.66 ns |    7 |
|   Insert |      100 |      0.03 |      True |  93.83 ns | 0.3026 ns | 0.2363 ns |  93.92 ns |    8 |
|   Insert |      100 |     0.001 |      True |  91.25 ns | 0.5187 ns | 0.4598 ns |  91.36 ns |    7 |
|   Insert |     1000 |      0.01 |      True |  90.53 ns | 0.6060 ns | 0.5669 ns |  90.54 ns |    7 |
|   Insert |     1000 |      0.03 |      True |  90.76 ns | 0.7821 ns | 0.7316 ns |  90.60 ns |    7 |
|   Insert |     1000 |     0.001 |      True |  90.82 ns | 0.3595 ns | 0.3002 ns |  90.72 ns |    7 |
|   Insert |    10000 |      0.01 |      True |  99.86 ns | 2.0277 ns | 3.6042 ns |  99.02 ns |    9 |
|   Insert |    10000 |      0.03 |      True | 103.25 ns | 2.0887 ns | 3.8192 ns | 103.11 ns |   10 |
|   Insert |    10000 |     0.001 |      True |  91.80 ns | 0.9089 ns | 0.8057 ns |  91.47 ns |    7 |
|   Insert |   100000 |      0.01 |      True |  99.01 ns | 2.0031 ns | 5.3811 ns |  96.95 ns |    9 |
|   Insert |   100000 |      0.03 |      True |  96.47 ns | 1.9403 ns | 2.0761 ns |  95.95 ns |    9 |
|   Insert |   100000 |     0.001 |      True | 106.25 ns | 2.1474 ns | 4.3866 ns | 105.75 ns |   11 |
|   Insert |  1000000 |      0.01 |      True | 121.85 ns | 2.6551 ns | 4.6503 ns | 120.91 ns |   12 |
|   Insert |  1000000 |      0.03 |      True | 124.15 ns | 2.5152 ns | 7.1353 ns | 121.57 ns |   12 |
|   Insert |  1000000 |     0.001 |      True | 126.61 ns | 2.5410 ns | 5.6832 ns | 126.06 ns |   13 |
| Contains |      100 |      0.01 |      True |  60.18 ns | 0.5445 ns | 0.5094 ns |  59.94 ns |    1 |
| Contains |      100 |      0.03 |      True |  60.21 ns | 0.4808 ns | 0.4498 ns |  60.23 ns |    1 |
| Contains |      100 |     0.001 |      True |  60.23 ns | 0.9332 ns | 0.8729 ns |  59.96 ns |    1 |
| Contains |     1000 |      0.01 |      True |  60.26 ns | 0.7890 ns | 0.7381 ns |  59.98 ns |    1 |
| Contains |     1000 |      0.03 |      True |  60.52 ns | 1.1586 ns | 1.0837 ns |  60.16 ns |    1 |
| Contains |     1000 |     0.001 |      True |  60.34 ns | 0.5887 ns | 0.5507 ns |  60.08 ns |    1 |
| Contains |    10000 |      0.01 |      True |  68.91 ns | 1.4057 ns | 3.9185 ns |  67.78 ns |    4 |
| Contains |    10000 |      0.03 |      True |  66.50 ns | 1.3453 ns | 3.0639 ns |  66.39 ns |    3 |
| Contains |    10000 |     0.001 |      True |  63.47 ns | 0.8225 ns | 0.7291 ns |  63.16 ns |    2 |
| Contains |   100000 |      0.01 |      True |  72.03 ns | 1.4671 ns | 3.8132 ns |  71.78 ns |    5 |
| Contains |   100000 |      0.03 |      True |  66.11 ns | 1.3499 ns | 2.2923 ns |  65.15 ns |    3 |
| Contains |   100000 |     0.001 |      True |  71.10 ns | 1.4520 ns | 2.7974 ns |  70.85 ns |    5 |
| Contains |  1000000 |      0.01 |      True |  85.85 ns | 2.2149 ns | 6.4258 ns |  83.77 ns |    6 |
| Contains |  1000000 |      0.03 |      True |  84.92 ns | 2.0583 ns | 5.9716 ns |  82.90 ns |    6 |
| Contains |  1000000 |     0.001 |      True |  85.69 ns | 1.7265 ns | 4.5784 ns |  85.34 ns |    6 |
```

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft
trademarks or logos is subject to and must follow
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
