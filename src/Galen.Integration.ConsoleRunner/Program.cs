using Azure.Storage.Blobs;
using Galen.Integration.Application.Services;
using Galen.Integration.Infrastructure.Blob;
using Galen.Integration.Infrastructure.Csv;
using Galen.Integration.Infrastructure.Processing;
using Galen.Integration.Infrastructure.Sql;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Simple console runner to process a local CSV file (no Azure Functions/Azurite needed)
// Usage: dotnet run -- sample-data/sample.csv
//        dotnet run -- sample-data/sample.csv --use-sql      (local SQL)
//        dotnet run -- sample-data/sample.csv --use-azure    (Azure SQL + Azure Blob)

var csvPath = args.FirstOrDefault(a => !a.StartsWith("--")) ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "sample-data", "sample.csv");
if (!Path.IsPathRooted(csvPath))
    csvPath = Path.GetFullPath(csvPath);

if (!File.Exists(csvPath))
{
    Console.WriteLine($"File not found: {csvPath}");
    Console.WriteLine("Usage: dotnet run -- <path-to-csv>");
    return 1;
}

Console.WriteLine($"Processing: {csvPath}");
Console.WriteLine("---");

using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger("ConsoleRunner");

var csvOptions = Options.Create(new CsvProcessorOptions());
var fileOptions = Options.Create(new FileProcessorOptions());
var useSql = args.Contains("--use-sql");
var useAzure = args.Contains("--use-azure");

(string? blobConn, string? sqlConn) = useAzure ? LoadAzureConfig() : (null, null);

IRecordRepository recordRepo;
if (useSql || useAzure)
{
    var connStr = useAzure ? sqlConn : Environment.GetEnvironmentVariable("RecordRepository__ConnectionString");
    connStr ??= "Server=localhost\\SQLEXPRESS;Database=GalenIntegration;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;";
    var sqlOptions = Options.Create(new RecordRepositoryOptions { ConnectionString = connStr, CommandTimeoutSeconds = 120 });
    var sqlFactory = new SqlConnectionFactory(sqlOptions);
    recordRepo = new ResilientRecordRepository(new RecordRepository(loggerFactory.CreateLogger<RecordRepository>(), sqlOptions, sqlFactory));
}
else
{
    recordRepo = new Galen.Integration.ConsoleRunner.NoOpRecordRepository();
    Console.WriteLine("(Demo mode - SQL disabled. Use --use-sql or --use-azure to write to database)");
}

IBlobFileService blobService;
if (useAzure && !string.IsNullOrEmpty(blobConn))
{
    var containerName = Environment.GetEnvironmentVariable("BlobContainerName") ?? "inbound";
    var container = new BlobContainerClient(blobConn, containerName);
    var blobOptions = Options.Create(new BlobFileServiceOptions());
    blobService = new BlobFileService(container, loggerFactory.CreateLogger<BlobFileService>(), blobOptions);
    Console.WriteLine("(Azure mode - writing to Azure SQL and Azure Blob)");
}
else
{
    var outputDir = Path.GetDirectoryName(Path.GetFullPath(csvPath));
    blobService = new Galen.Integration.ConsoleRunner.NoOpBlobFileService(outputDir);
}

var csvProcessor = new CsvProcessor(loggerFactory.CreateLogger<CsvProcessor>(), csvOptions);

var fileProcessor = new FileProcessor(csvProcessor, recordRepo, blobService,
    loggerFactory.CreateLogger<FileProcessor>(), fileOptions);

try
{
    await using var stream = File.OpenRead(csvPath);
    var fileName = Path.GetFileName(csvPath);
    var result = await fileProcessor.ProcessFileAsync(stream, fileName);

    Console.WriteLine();
    Console.WriteLine("=== RESULT ===");
    Console.WriteLine($"File: {result.FileName}");
    Console.WriteLine($"Total rows: {result.TotalRowsRead}");
    Console.WriteLine($"Accepted: {result.RowsAccepted}");
    Console.WriteLine($"Rejected: {result.RowsRejected}");
    if (result.RejectedRows.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Rejected rows:");
        foreach (var r in result.RejectedRows)
            Console.WriteLine($"  Row {r.RowIndex}: {string.Join("; ", r.Errors)}");
    }
    Console.WriteLine("Done.");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 1;
}

static (string? blobConn, string? sqlConn) LoadAzureConfig()
{
    var baseDir = AppContext.BaseDirectory;
    for (int i = 0; i < 5; i++)
    {
        var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Galen.Integration.Functions", "local.settings.json"));
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var blobMatch = System.Text.RegularExpressions.Regex.Match(json, @"AzureWebJobsStorage""\s*:\s*""([^""]+)""");
            var sqlMatch = System.Text.RegularExpressions.Regex.Match(json, @"RecordRepository__ConnectionString""\s*:\s*""([^""]+)""");
            return (blobMatch.Success ? blobMatch.Groups[1].Value : null, sqlMatch.Success ? sqlMatch.Groups[1].Value : null);
        }
        baseDir = Path.GetDirectoryName(baseDir) ?? baseDir;
    }
    return (null, null);
}
