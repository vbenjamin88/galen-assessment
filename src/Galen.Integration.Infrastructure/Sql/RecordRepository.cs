using System.Data;
using Galen.Integration.Application.Services;
using Galen.Integration.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Galen.Integration.Infrastructure.Sql;

public sealed class RecordRepository : IRecordRepository
{
    private readonly ILogger<RecordRepository> _logger;
    private readonly RecordRepositoryOptions _options;
    private readonly SqlConnectionFactory _connectionFactory;

    public RecordRepository(
        ILogger<RecordRepository> logger,
        IOptions<RecordRepositoryOptions> options,
        SqlConnectionFactory connectionFactory)
    {
        _logger = logger;
        _options = options.Value;
        _connectionFactory = connectionFactory;
    }

    public async Task<int> SaveBatchAsync(
        IReadOnlyList<CanonicalRecord> records,
        string sourceFileName,
        CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
            return 0;

        await using var conn = _connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        var table = BuildDataTable(records, sourceFileName);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "dbo.usp_ImportCanonicalRecords";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;

        var param = cmd.Parameters.AddWithValue("@Records", table);
        param.SqlDbType = SqlDbType.Structured;
        param.TypeName = "dbo.CanonicalRecordType";

        var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Batch insert: {Count} records from {File}, rows affected: {Affected}",
            records.Count, sourceFileName, rowsAffected);

        return rowsAffected;
    }

    private static DataTable BuildDataTable(IReadOnlyList<CanonicalRecord> records, string sourceFileName)
    {
        var table = new DataTable();
        table.Columns.Add("ExternalId", typeof(string));
        table.Columns.Add("PatientIdentifier", typeof(string));
        table.Columns.Add("DocumentType", typeof(string));
        table.Columns.Add("DocumentDate", typeof(DateTime));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("SourceSystem", typeof(string));
        table.Columns.Add("SourceFile", typeof(string));
        table.Columns.Add("SourceRowIndex", typeof(int));

        foreach (var r in records)
        {
            var row = table.NewRow();
            row["ExternalId"] = (object?)r.ExternalId ?? DBNull.Value;
            row["PatientIdentifier"] = (object?)r.PatientIdentifier ?? DBNull.Value;
            row["DocumentType"] = (object?)r.DocumentType ?? DBNull.Value;
            row["DocumentDate"] = r.DocumentDate.HasValue ? (object)r.DocumentDate.Value : DBNull.Value;
            row["Description"] = (object?)r.Description ?? DBNull.Value;
            row["SourceSystem"] = (object?)r.SourceSystem ?? DBNull.Value;
            row["SourceFile"] = sourceFileName;
            row["SourceRowIndex"] = r.SourceRowIndex;
            table.Rows.Add(row);
        }

        return table;
    }
}

public sealed class RecordRepositoryOptions
{
    public const string SectionName = "RecordRepository";
    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 120;
}
