using Galen.Integration.Domain;

namespace Galen.Integration.Application.Validation;

/// <summary>
/// Validates and normalizes CsvRowInput into CanonicalRecord.
/// Returns validation errors for rejected rows.
/// </summary>
public static class RecordValidator
{
    private const int MaxFieldLength = 500;
    private static readonly string[] AllowedDocTypes = ["Lab", "Radiology", "Clinical", "Administrative", "Other"];

    public static (CanonicalRecord? Record, List<string> Errors) ValidateAndNormalize(CsvRowInput input, int rowIndex)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(input.Id))
            errors.Add("Id is required");
        else if (input.Id.Length > MaxFieldLength)
            errors.Add($"Id exceeds max length ({MaxFieldLength})");

        if (string.IsNullOrWhiteSpace(input.PatientId))
            errors.Add("PatientId is required");
        else if (input.PatientId.Length > MaxFieldLength)
            errors.Add($"PatientId exceeds max length ({MaxFieldLength})");

        if (!string.IsNullOrWhiteSpace(input.DocType) && !AllowedDocTypes.Contains(input.DocType.Trim(), StringComparer.OrdinalIgnoreCase))
            errors.Add($"DocType must be one of: {string.Join(", ", AllowedDocTypes)}");

        DateTime? docDate = null;
        if (!string.IsNullOrWhiteSpace(input.DocDate))
        {
            if (DateTime.TryParse(input.DocDate, out var parsed))
                docDate = parsed;
            else
                errors.Add("DocDate must be a valid date");
        }

        if (input.Description != null && input.Description.Length > 1000)
            errors.Add($"Description exceeds max length (1000)");

        if (input.SourceSystem != null && input.SourceSystem.Length > MaxFieldLength)
            errors.Add($"SourceSystem exceeds max length ({MaxFieldLength})");

        if (errors.Count > 0)
            return (null, errors);

        return (new CanonicalRecord
        {
            ExternalId = input.Id!.Trim(),
            PatientIdentifier = input.PatientId!.Trim(),
            DocumentType = input.DocType?.Trim() ?? "Other",
            DocumentDate = docDate,
            Description = input.Description?.Trim(),
            SourceSystem = input.SourceSystem?.Trim(),
            SourceFile = null, // Set by processor
            SourceRowIndex = rowIndex
        }, []);
    }
}
