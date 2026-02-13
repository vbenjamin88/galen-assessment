using Azure.Storage.Blobs;
using Galen.Integration.Application.Services;
using Galen.Integration.Infrastructure.Blob;
using Galen.Integration.Infrastructure.Csv;
using Galen.Integration.Infrastructure.Processing;
using Galen.Integration.Infrastructure.Sql;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddEnvironmentVariables();
        config.AddJsonFile("local.settings.json", optional: true);
    })
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        services.Configure<BlobFileServiceOptions>(config.GetSection(BlobFileServiceOptions.SectionName));
        services.Configure<CsvProcessorOptions>(config.GetSection("CsvProcessor"));
        services.Configure<FileProcessorOptions>(config.GetSection(FileProcessorOptions.SectionName));
        services.Configure<RecordRepositoryOptions>(config.GetSection(RecordRepositoryOptions.SectionName));

        var blobConnection = config["AzureWebJobsStorage"] ?? config["BlobStorage__ConnectionString"] ?? "";
        var containerName = config["BlobContainerName"] ?? "inbound";
        services.AddSingleton(_ => new BlobContainerClient(blobConnection, containerName));

        services.AddSingleton<SqlConnectionFactory>();
        services.AddSingleton<ICsvProcessor, CsvProcessor>();
        services.AddSingleton<RecordRepository>();
        services.AddSingleton<IRecordRepository>(sp => new ResilientRecordRepository(sp.GetRequiredService<RecordRepository>()));
        services.AddSingleton<IBlobFileService, BlobFileService>();
        services.AddSingleton<IFileProcessor, FileProcessor>();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddSerilog();
    })
    .Build();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Galen.Integration")
    .WriteTo.Console()
    .CreateLogger();

await host.RunAsync();
