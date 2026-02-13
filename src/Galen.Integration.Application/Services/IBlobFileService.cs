namespace Galen.Integration.Application.Services;

/// <summary>
/// Blob storage operations for file lifecycle management.
/// </summary>
public interface IBlobFileService
{
    /// <summary>
    /// Acquire lease for exclusive processing (concurrency control).
    /// </summary>
    Task<string?> AcquireLeaseAsync(string blobName, int leaseDurationSeconds = 60, CancellationToken ct = default);

    /// <summary>
    /// Release lease after processing.
    /// </summary>
    Task ReleaseLeaseAsync(string blobName, string leaseId, CancellationToken ct = default);

    /// <summary>
    /// Check if blob was already processed (idempotency).
    /// </summary>
    Task<bool> IsAlreadyProcessedAsync(string blobName, CancellationToken ct = default);

    /// <summary>
    /// Mark blob as processed (set metadata).
    /// </summary>
    Task MarkProcessedAsync(string blobName, string processingId, CancellationToken ct = default);

    /// <summary>
    /// Move blob to processed/ prefix.
    /// </summary>
    Task MoveToProcessedAsync(string blobName, CancellationToken ct = default);

    /// <summary>
    /// Move blob to quarantine/ with error context.
    /// </summary>
    Task MoveToQuarantineAsync(string blobName, string errorSummary, CancellationToken ct = default);

    /// <summary>
    /// Write companion .errors.json file for rejected rows.
    /// </summary>
    Task WriteErrorsFileAsync(string blobName, string errorsJson, CancellationToken ct = default);
}
