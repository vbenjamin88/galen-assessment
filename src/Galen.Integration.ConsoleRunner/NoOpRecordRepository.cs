using Galen.Integration.Application.Services;
using Galen.Integration.Domain;

namespace Galen.Integration.ConsoleRunner;

public sealed class NoOpRecordRepository : IRecordRepository
{
    public Task<int> SaveBatchAsync(IReadOnlyList<CanonicalRecord> records, string sourceFileName, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[SQL skipped] Would insert {records.Count} records");
        return Task.FromResult(records.Count);
    }
}
