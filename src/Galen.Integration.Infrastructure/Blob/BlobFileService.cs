using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Galen.Integration.Application.Services;
using Galen.Integration.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Galen.Integration.Infrastructure.Blob;

public sealed class BlobFileService : IBlobFileService
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<BlobFileService> _logger;
    private readonly BlobFileServiceOptions _options;

    public BlobFileService(
        BlobContainerClient container,
        ILogger<BlobFileService> logger,
        IOptions<BlobFileServiceOptions> options)
    {
        _container = container;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string?> AcquireLeaseAsync(string blobName, int leaseDurationSeconds = 60, CancellationToken ct = default)
    {
        try
        {
            var client = _container.GetBlobClient(blobName);
            var leaseClient = client.GetBlobLeaseClient();
            var lease = await leaseClient.AcquireAsync(TimeSpan.FromSeconds(leaseDurationSeconds), cancellationToken: ct);
            _logger.LogDebug("Acquired lease for {Blob}", blobName);
            return lease.Value.LeaseId;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            _logger.LogWarning("Could not acquire lease for {Blob} - already leased", blobName);
            return null;
        }
    }

    public async Task ReleaseLeaseAsync(string blobName, string leaseId, CancellationToken ct = default)
    {
        try
        {
            var client = _container.GetBlobClient(blobName);
            var leaseClient = client.GetBlobLeaseClient(leaseId);
            await leaseClient.ReleaseAsync(cancellationToken: ct);
            _logger.LogDebug("Released lease for {Blob}", blobName);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Failed to release lease for {Blob}", blobName);
        }
    }

    public async Task<bool> IsAlreadyProcessedAsync(string blobName, CancellationToken ct = default)
    {
        try
        {
            var client = _container.GetBlobClient(blobName);
            var props = await client.GetPropertiesAsync(cancellationToken: ct);
            return props.Value.Metadata.TryGetValue("ProcessedAt", out _);
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task MarkProcessedAsync(string blobName, string processingId, CancellationToken ct = default)
    {
        var client = _container.GetBlobClient(blobName);
        var metadata = new Dictionary<string, string>
        {
            ["ProcessedAt"] = DateTime.UtcNow.ToString("O"),
            ["ProcessingId"] = processingId
        };
        await client.SetMetadataAsync(metadata, cancellationToken: ct);
    }

    public async Task MoveToProcessedAsync(string blobName, CancellationToken ct = default)
    {
        var destName = $"{_options.ProcessedPrefix.TrimEnd('/')}/{DateTime.UtcNow:yyyy/MM/dd}/{blobName}";
        await CopyAndDeleteAsync(blobName, destName, ct);
    }

    public async Task MoveToQuarantineAsync(string blobName, string errorSummary, CancellationToken ct = default)
    {
        var destName = $"{_options.QuarantinePrefix.TrimEnd('/')}/{DateTime.UtcNow:yyyyMMddHHmmss}-{Path.GetFileName(blobName)}";
        var client = _container.GetBlobClient(blobName);
        var destClient = _container.GetBlobClient(destName);

        await destClient.StartCopyFromUriAsync(client.Uri, cancellationToken: ct);
        await WaitForCopyAsync(destClient, ct);
        await destClient.SetMetadataAsync(new Dictionary<string, string> { ["ErrorSummary"] = errorSummary }, cancellationToken: ct);
        await client.DeleteIfExistsAsync(cancellationToken: ct);
        _logger.LogWarning("Moved {Blob} to quarantine: {Error}", blobName, errorSummary);
    }

    public async Task WriteErrorsFileAsync(string blobName, string errorsJson, CancellationToken ct = default)
    {
        var baseName = Path.GetFileNameWithoutExtension(blobName);
        var errorsName = $"{baseName}.errors.json";
        var client = _container.GetBlobClient(errorsName);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(errorsJson));
        await client.UploadAsync(stream, overwrite: true, cancellationToken: ct);
        _logger.LogInformation("Wrote errors file {File} with {Bytes} bytes", errorsName, errorsJson.Length);
    }

    private async Task CopyAndDeleteAsync(string sourceName, string destName, CancellationToken ct)
    {
        var source = _container.GetBlobClient(sourceName);
        var dest = _container.GetBlobClient(destName);
        await dest.StartCopyFromUriAsync(source.Uri, cancellationToken: ct);
        await WaitForCopyAsync(dest, ct);
        await source.DeleteIfExistsAsync(cancellationToken: ct);
    }

    private static async Task WaitForCopyAsync(BlobClient client, CancellationToken ct)
    {
        while (true)
        {
            var props = await client.GetPropertiesAsync(cancellationToken: ct);
            if (props.Value.CopyStatus != CopyStatus.Pending)
                break;
            await Task.Delay(500, ct);
        }
    }
}

public sealed class BlobFileServiceOptions
{
    public const string SectionName = "BlobFileService";
    public string ProcessedPrefix { get; set; } = "processed";
    public string QuarantinePrefix { get; set; } = "quarantine";
}
