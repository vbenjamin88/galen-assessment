using Galen.Integration.Domain;

namespace Galen.Integration.Application.Services;

/// <summary>
/// Persists canonical records to the target store. Uses batch operations.
/// </summary>
public interface IRecordRepository
{
    /// <summary>
    /// Batch insert/merge records. Idempotent when SourceFile + SourceRowIndex form a unique key.
    /// </summary>
    Task<int> SaveBatchAsync(
        IReadOnlyList<CanonicalRecord> records,
        string sourceFileName,
        CancellationToken cancellationToken = default);
}
