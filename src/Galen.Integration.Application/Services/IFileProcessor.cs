using Galen.Integration.Domain;

namespace Galen.Integration.Application.Services;

/// <summary>
/// Orchestrates the full file processing workflow.
/// </summary>
public interface IFileProcessor
{
    Task<ProcessingResult> ProcessFileAsync(
        Stream fileStream,
        string blobName,
        CancellationToken cancellationToken = default);
}
