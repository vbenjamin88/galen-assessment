using System.Text;
using Azure.Storage.Blobs;
using FluentAssertions;
using Galen.Integration.Application.Services;
using Galen.Integration.Domain;
using Galen.Integration.Infrastructure.Blob;
using Galen.Integration.Infrastructure.Csv;
using Galen.Integration.Infrastructure.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Galen.Integration.Tests.Integration;

/// <summary>
/// Integration tests that run against Azurite (start via Azurite Action in CI or locally).
/// </summary>
public class CsvProcessingIntegrationTests
{
    private static string ConnectionString => Environment.GetEnvironmentVariable("AzureWebJobsStorage")
        ?? "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessCsv_AgainstAzurite_ReadsAndValidates()
    {
        var container = new BlobContainerClient(ConnectionString, "test-inbound-" + Guid.NewGuid().ToString("N")[..8]);
        await container.CreateIfNotExistsAsync();

        var csv = @"id,patient_id,doc_type,doc_date,description,source_system
EXT-001,PAT-10001,Lab,2024-01-15,CBC,EMR
EXT-002,PAT-10002,,2024-01-16,X-Ray,EMR";
        var blob = container.GetBlobClient("sample.csv");
        await blob.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes(csv)), overwrite: true);

        var logger = Substitute.For<ILogger<CsvProcessor>>();
        var csvProcessor = new CsvProcessor(logger, Options.Create(new CsvProcessorOptions()));
        var records = new List<CanonicalRecord>();
        var rejected = new List<RejectedRow>();

        await using var stream = await blob.OpenReadAsync();
        await foreach (var (record, rej) in csvProcessor.ProcessAsync(stream, "sample.csv"))
        {
            if (record != null) records.Add(record);
            if (rej != null) rejected.Add(rej);
        }

        records.Should().HaveCount(2);
        rejected.Should().BeEmpty();
        records[0].ExternalId.Should().Be("EXT-001");
        records[1].DocumentType.Should().Be("Other");

        await container.DeleteIfExistsAsync();
    }
}
