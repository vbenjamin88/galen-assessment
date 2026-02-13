using FluentAssertions;
using Galen.Integration.Application.Validation;
using Galen.Integration.Domain;
using Xunit;

namespace Galen.Integration.Tests.Unit;

public class RecordValidatorTests
{
    [Fact]
    public void ValidateAndNormalize_ValidRow_ReturnsRecord()
    {
        var input = new CsvRowInput
        {
            Id = "EXT-001",
            PatientId = "PAT-10001",
            DocType = "Lab",
            DocDate = "2024-01-15",
            Description = "CBC Panel",
            SourceSystem = "PartnerEMR"
        };

        var (record, errors) = RecordValidator.ValidateAndNormalize(input, 1);

        record.Should().NotBeNull();
        record!.ExternalId.Should().Be("EXT-001");
        record.PatientIdentifier.Should().Be("PAT-10001");
        record.DocumentType.Should().Be("Lab");
        record.DocumentDate.Should().Be(new DateTime(2024, 1, 15));
        record.Description.Should().Be("CBC Panel");
        record.SourceSystem.Should().Be("PartnerEMR");
        record.SourceRowIndex.Should().Be(1);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAndNormalize_MissingId_ReturnsErrors()
    {
        var input = new CsvRowInput
        {
            Id = "",
            PatientId = "PAT-10001",
            DocType = "Lab",
            DocDate = "2024-01-15"
        };

        var (record, errors) = RecordValidator.ValidateAndNormalize(input, 2);

        record.Should().BeNull();
        errors.Should().Contain("Id is required");
    }

    [Fact]
    public void ValidateAndNormalize_MissingPatientId_ReturnsErrors()
    {
        var input = new CsvRowInput
        {
            Id = "EXT-001",
            PatientId = "",
            DocType = "Lab"
        };

        var (record, errors) = RecordValidator.ValidateAndNormalize(input, 3);

        record.Should().BeNull();
        errors.Should().Contain("PatientId is required");
    }

    [Fact]
    public void ValidateAndNormalize_InvalidDocType_ReturnsErrors()
    {
        var input = new CsvRowInput
        {
            Id = "EXT-001",
            PatientId = "PAT-10001",
            DocType = "InvalidType",
            DocDate = "2024-01-15"
        };

        var (record, errors) = RecordValidator.ValidateAndNormalize(input, 4);

        record.Should().BeNull();
        errors.Should().Contain(e => e.Contains("DocType"));
    }

    [Fact]
    public void ValidateAndNormalize_InvalidDate_ReturnsErrors()
    {
        var input = new CsvRowInput
        {
            Id = "EXT-001",
            PatientId = "PAT-10001",
            DocType = "Lab",
            DocDate = "not-a-date"
        };

        var (record, errors) = RecordValidator.ValidateAndNormalize(input, 5);

        record.Should().BeNull();
        errors.Should().Contain("DocDate must be a valid date");
    }

    [Fact]
    public void ValidateAndNormalize_NullDocType_DefaultsToOther()
    {
        var input = new CsvRowInput
        {
            Id = "EXT-001",
            PatientId = "PAT-10001",
            DocType = null,
            DocDate = "2024-01-15"
        };

        var (record, errors) = RecordValidator.ValidateAndNormalize(input, 6);

        record.Should().NotBeNull();
        record!.DocumentType.Should().Be("Other");
    }
}
