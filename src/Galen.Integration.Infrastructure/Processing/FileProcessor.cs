using System.Diagnostics;
using System.Text.Json;
using Galen.Integration.Application.Services;
using Galen.Integration.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Galen.Integration.Infrastructure.Processing;

public sealed class FileProcessor : IFileProcessor
{
    private readonly ICsvProcessor _csvProcessor;
    private readonly IRecordRepository _recordRepository;
    private readonly IBlobFileService _blobFileService;
    private readonly ILogger<FileProcessor> _logger;
    private readonly FileProcessorOptions _options;

    public FileProcessor(
        ICsvProcessor csvProcessor,
        IRecordRepository recordRepository,
        IBlobFileService blobFileService,
        ILogger<FileProcessor> logger,
        IOptions<FileProcessorOptions> options)
    {
        _csvProcessor = csvProcessor;
        _recordRepository = recordRepository;
        _blobFileService = blobFileService;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<ProcessingResult> ProcessFileAsync(
        Stream fileStream,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ProcessingResult { FileName = blobName };
        var batch = new List<CanonicalRecord>(_options.BatchSize);
        var rejected = new List<RejectedRow>();

        try
        {
            await foreach (var (record, rejectedRow) in _csvProcessor.ProcessAsync(fileStream, blobName, cancellationToken))
            {
                if (record != null)
                {
                    batch.Add(record);
                    if (batch.Count >= _options.BatchSize)
                    {
                        var count = await _recordRepository.SaveBatchAsync(batch, blobName, cancellationToken);
                        result.RowsAccepted += count;
                        batch.Clear();
                    }
                }
                else if (rejectedRow != null)
                {
                    rejected.Add(rejectedRow);
                }
            }

            if (batch.Count > 0)
            {
                var count = await _recordRepository.SaveBatchAsync(batch, blobName, cancellationToken);
                result.RowsAccepted += count;
            }

            result.RejectedRows = rejected;
            result.RowsRejected = rejected.Count;
            result.TotalRowsRead = result.RowsAccepted + result.RowsRejected;

            if (rejected.Count > 0)
            {
                var errorsJson = JsonSerializer.Serialize(
                    new ErrorsFileFormat(blobName, DateTime.UtcNow, rejected.Count,
                        rejected.Select(r => new RejectedRowDto(r.RowIndex, r.RawLine ?? "", r.Errors.ToList())).ToList()),
                    new JsonSerializerOptions { WriteIndented = true });
                await _blobFileService.WriteErrorsFileAsync(blobName, errorsJson, cancellationToken);
            }

            sw.Stop();
            _logger.LogInformation(
                "Processed {File}: {Accepted} accepted, {Rejected} rejected, {Total} total, {Duration}ms",
                blobName, result.RowsAccepted, result.RowsRejected, result.TotalRowsRead, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {File}", blobName);
            throw;
        }

        return result;
    }

    private sealed record ErrorsFileFormat(
        string SourceFile,
        DateTime ProcessedAt,
        int TotalRejected,
        List<RejectedRowDto> RejectedRows);

    private sealed record RejectedRowDto(int RowIndex, string? RawLine, List<string> Errors);
}

public sealed class FileProcessorOptions
{
    public const string SectionName = "FileProcessor";
    public int BatchSize { get; set; } = 500;
}
