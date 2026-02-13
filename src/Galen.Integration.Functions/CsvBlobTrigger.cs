using System.Diagnostics;
using Azure.Storage.Blobs;
using Galen.Integration.Application.Services;
using Galen.Integration.Domain;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Galen.Integration.Functions;

/// <summary>
/// Azure Function triggered when a CSV file is uploaded to the inbound container.
/// Implements idempotency, concurrency control via lease, and full processing workflow.
/// </summary>
public class CsvBlobTrigger
{
    private readonly IFileProcessor _fileProcessor;
    private readonly IBlobFileService _blobFileService;
    private readonly ILogger<CsvBlobTrigger> _logger;

    public CsvBlobTrigger(
        IFileProcessor fileProcessor,
        IBlobFileService blobFileService,
        ILogger<CsvBlobTrigger> logger)
    {
        _fileProcessor = fileProcessor;
        _blobFileService = blobFileService;
        _logger = logger;
    }

    [Function("CsvBlobTrigger")]
    public async Task Run(
        [BlobTrigger("inbound/{name}.csv", Connection = "AzureWebJobsStorage")] BlobClient blobClient,
        string name,
        CancellationToken cancellationToken)
    {
        var blobName = $"{name}.csv";
        var processingId = $"{blobName}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var activity = new Activity("ProcessCsvFile");
        activity.SetTag("blob.name", blobName);
        activity.SetTag("processing.id", processingId);
        activity.Start();

        try
        {
            // Idempotency: skip if already processed
            if (await _blobFileService.IsAlreadyProcessedAsync(blobName, cancellationToken))
            {
                _logger.LogInformation("Blob {Blob} already processed, skipping (idempotent)", blobName);
                return;
            }

            // Concurrency control: acquire lease
            var leaseId = await _blobFileService.AcquireLeaseAsync(blobName, 60, cancellationToken);
            if (leaseId == null)
            {
                _logger.LogWarning("Could not acquire lease for {Blob}, another instance may be processing", blobName);
                return;
            }

            try
            {
                await using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);

                var result = await _fileProcessor.ProcessFileAsync(stream, blobName, cancellationToken);

                await _blobFileService.MarkProcessedAsync(blobName, processingId, cancellationToken);
                await _blobFileService.MoveToProcessedAsync(blobName, cancellationToken);

                _logger.LogInformation(
                    "Successfully processed {Blob}: {Accepted} accepted, {Rejected} rejected",
                    blobName, result.RowsAccepted, result.RowsRejected);
            }
            finally
            {
                await _blobFileService.ReleaseLeaseAsync(blobName, leaseId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {Blob}", blobName);
            try
            {
                await _blobFileService.MoveToQuarantineAsync(blobName, ex.Message, cancellationToken);
            }
            catch (Exception quarantineEx)
            {
                _logger.LogError(quarantineEx, "Failed to move {Blob} to quarantine", blobName);
            }
            throw;
        }
        finally
        {
            activity?.Stop();
        }
    }
}
