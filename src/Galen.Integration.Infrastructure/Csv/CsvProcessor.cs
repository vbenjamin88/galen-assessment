using System.Globalization;
using System.Text;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using Galen.Integration.Application.Services;
using Galen.Integration.Application.Validation;
using Galen.Integration.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Galen.Integration.Infrastructure.Csv;

public sealed class CsvProcessor : ICsvProcessor
{
    private readonly ILogger<CsvProcessor> _logger;
    private readonly CsvProcessorOptions _options;

    public CsvProcessor(ILogger<CsvProcessor> logger, IOptions<CsvProcessorOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async IAsyncEnumerable<(CanonicalRecord? Record, RejectedRow? Rejected)> ProcessAsync(
        Stream csvStream,
        string sourceFileName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            AllowComments = false
        };

        var encoding = GetEncoding(csvStream);
        using var reader = new StreamReader(csvStream, encoding, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        csv.Context.RegisterClassMap<CsvRowInputMap>();

        await csv.ReadAsync();
        csv.ReadHeader();

        var rowIndex = 1; // 1-based after header
        var totalRows = 0;

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalRows++;

            if (totalRows > _options.MaxRowsPerFile)
            {
                _logger.LogWarning("File {File} exceeded max rows ({Max}), stopping", sourceFileName, _options.MaxRowsPerFile);
                yield return (null, new RejectedRow
                {
                    RowIndex = rowIndex,
                    RawLine = $"[Truncated - max rows {_options.MaxRowsPerFile} exceeded]",
                    Errors = [$"File exceeded maximum allowed rows ({_options.MaxRowsPerFile})"]
                });
                yield break;
            }

            CsvRowInput? input = null;
            string? rawLine = null;

            RejectedRow? parseError = null;
            try
            {
                input = csv.GetRecord<CsvRowInput>();
                rawLine = csv.Context.Parser?.RawRecord;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Parse error at row {Row}", rowIndex);
                parseError = new RejectedRow { RowIndex = rowIndex, RawLine = rawLine ?? "[unable to capture]", Errors = [$"Parse error: {ex.Message}"] };
            }
            if (parseError != null)
            {
                yield return (null, parseError);
                rowIndex++;
                continue;
            }

            if (input == null)
            {
                yield return (null, new RejectedRow
                {
                    RowIndex = rowIndex,
                    RawLine = rawLine ?? "",
                    Errors = ["Empty or null row"]
                });
                rowIndex++;
                continue;
            }

            var (record, errors) = RecordValidator.ValidateAndNormalize(input, rowIndex);

            if (record != null)
            {
                record.SourceFile = sourceFileName;
                yield return (record, null);
            }
            else
            {
                yield return (null, new RejectedRow
                {
                    RowIndex = rowIndex,
                    RawLine = rawLine ?? string.Join(",", input.Id, input.PatientId, input.DocType, input.DocDate, input.Description, input.SourceSystem),
                    Errors = errors
                });
            }

            rowIndex++;
        }
    }

    private static Encoding GetEncoding(Stream stream)
    {
        if (!stream.CanSeek)
            return Encoding.UTF8;
        try
        {
            var buffer = new byte[Math.Min(4096, stream.Length > 0 ? (int)stream.Length : 4096)];
            var read = stream.Read(buffer, 0, buffer.Length);
            stream.Position = 0;
            if (read >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                return Encoding.UTF8;
        }
        catch { /* fallback to UTF8 */ }
        return Encoding.UTF8;
    }
}

/// <summary>
/// Options for CSV processing.
/// </summary>
public sealed class CsvProcessorOptions
{
    public int MaxRowsPerFile { get; set; } = 100_000;
    public int MaxRowLengthBytes { get; set; } = 10_000;
}
