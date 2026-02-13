using Galen.Integration.Application.Services;

namespace Galen.Integration.ConsoleRunner;

public sealed class NoOpBlobFileService : IBlobFileService
{
    private readonly string? _outputDirectory;

    public NoOpBlobFileService(string? outputDirectory = null) => _outputDirectory = outputDirectory;

    public Task<string?> AcquireLeaseAsync(string blobName, int leaseDurationSeconds = 60, CancellationToken ct = default) => Task.FromResult<string?>("console");
    public Task ReleaseLeaseAsync(string blobName, string leaseId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> IsAlreadyProcessedAsync(string blobName, CancellationToken ct = default) => Task.FromResult(false);
    public Task MarkProcessedAsync(string blobName, string processingId, CancellationToken ct = default) => Task.CompletedTask;
    public Task MoveToProcessedAsync(string blobName, CancellationToken ct = default) => Task.CompletedTask;
    public Task MoveToQuarantineAsync(string blobName, string errorSummary, CancellationToken ct = default) => Task.CompletedTask;
    public Task WriteErrorsFileAsync(string blobName, string errorsJson, CancellationToken ct = default)
    {
        var baseName = Path.GetFileNameWithoutExtension(blobName);
        var errorsFileName = $"{baseName}.errors.json";
        // Write next to CSV (if path has directory) or in current directory
        var outputPath = _outputDirectory != null
            ? Path.Combine(_outputDirectory, errorsFileName)
            : Path.Combine(Directory.GetCurrentDirectory(), errorsFileName);
        File.WriteAllText(outputPath, errorsJson);
        Console.WriteLine($"Created: {outputPath}");
        return Task.CompletedTask;
    }

}
