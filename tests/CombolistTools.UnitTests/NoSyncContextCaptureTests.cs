using System.Threading;
using CombolistTools.Application;
using CombolistTools.Core;
using CombolistTools.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace CombolistTools.UnitTests;

public sealed class NoSyncContextCaptureTests
{
    private sealed class ThrowingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) =>
            throw new InvalidOperationException("SynchronizationContext.Post should not be used.");

        public override void Send(SendOrPostCallback d, object? state) =>
            throw new InvalidOperationException("SynchronizationContext.Send should not be used.");
    }

    [Fact]
    public async Task DuplicateRemoverService_Completes_WhenSyncContextPostSendThrow()
    {
        var original = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new ThrowingSynchronizationContext());

        try
        {
            var dir = Path.Combine(Path.GetTempPath(), $"comb-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            var input = Path.Combine(dir, "input.csv");
            var output = Path.Combine(dir, "output.csv");

            await File.WriteAllLinesAsync(input, ["A", "B", "A", "C"]).ConfigureAwait(false);

            var svc = new DuplicateRemoverService(
                new AutoDetectFileReader(),
                new AutoDetectFileWriter(),
                new ChunkProcessor(),
                (idx, delimiter) => new CsvDuplicateKeyStrategy(idx, delimiter),
                () => new BloomBackedDuplicateDetector(new BloomFilter(1000, 0.01)),
                new ProcessingOptions
                {
                    EnableParallel = true,
                    PreserveOutputOrder = true,
                    ChunkSize = 2,
                    MaxDegreeOfParallelism = 2
                },
                NullLogger<DuplicateRemoverService>.Instance);

            await svc.ExecuteAsync(
                new DuplicateRemovalOptions { InputPath = input, OutputPath = output },
                CancellationToken.None).ConfigureAwait(false);

            var lines = await File.ReadAllLinesAsync(output).ConfigureAwait(false);
            Assert.Equal(["A", "B", "C"], lines);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
        }
    }
}

