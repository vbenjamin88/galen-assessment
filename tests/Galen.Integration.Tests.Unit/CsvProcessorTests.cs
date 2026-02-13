using System.Text;
using FluentAssertions;
using Galen.Integration.Application.Services;
using Galen.Integration.Infrastructure.Csv;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Galen.Integration.Tests.Unit;

public class CsvProcessorTests
{
    private readonly ICsvProcessor _processor;

    public CsvProcessorTests()
    {
        var logger = Substitute.For<ILogger<CsvProcessor>>();
        var options = Options.Create(new CsvProcessorOptions { MaxRowsPerFile = 1000 });
        _processor = new CsvProcessor(logger, options);
    }

    [Fact]
    public async Task ProcessAsync_ValidCsv_YieldsRecords()
    {
        var csv = @"id,patient_id,doc_type,doc_date,description,source_system
EXT-001,PAT-10001,Lab,2024-01-15,CBC,EMR";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var records = new List<Galen.Integration.Domain.CanonicalRecord>();
        var rejected = new List<Galen.Integration.Domain.RejectedRow>();

        await foreach (var (record, rej) in _processor.ProcessAsync(stream, "test.csv"))
        {
            if (record != null) records.Add(record);
            if (rej != null) rejected.Add(rej);
        }

        records.Should().HaveCount(1);
        records[0].ExternalId.Should().Be("EXT-001");
        records[0].PatientIdentifier.Should().Be("PAT-10001");
        rejected.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_InvalidRow_YieldsRejected()
    {
        var csv = @"id,patient_id,doc_type,doc_date,description,source_system
EXT-001,,Lab,2024-01-15,CBC,EMR";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var rejected = new List<Galen.Integration.Domain.RejectedRow>();

        await foreach (var (_, rej) in _processor.ProcessAsync(stream, "test.csv"))
        {
            if (rej != null) rejected.Add(rej);
        }

        rejected.Should().HaveCount(1);
        rejected[0].Errors.Should().Contain("PatientId is required");
    }

    [Fact]
    public async Task ProcessAsync_MultipleRows_ProcessesAll()
    {
        var csv = @"id,patient_id,doc_type,doc_date,description,source_system
EXT-001,PAT-1,Lab,2024-01-15,A,EMR
EXT-002,PAT-2,Radiology,2024-01-16,B,EMR";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var count = 0;
        await foreach (var (record, _) in _processor.ProcessAsync(stream, "test.csv"))
        {
            if (record != null) count++;
        }

        count.Should().Be(2);
    }
}
