using Galen.Integration.Domain;

namespace Galen.Integration.Application.Services;

/// <summary>
/// Processes CSV content: parse, validate, normalize. Stream-based for memory efficiency.
/// </summary>
public interface ICsvProcessor
{
    /// <summary>
    /// Process CSV stream. Yields (canonical record, null) for valid rows or (null, rejected row) for invalid.
    /// </summary>
    IAsyncEnumerable<(CanonicalRecord? Record, RejectedRow? Rejected)> ProcessAsync(
        Stream csvStream,
        string sourceFileName,
        CancellationToken cancellationToken = default);
}
