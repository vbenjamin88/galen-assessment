namespace Galen.Integration.Domain;

/// <summary>
/// Canonical record after normalization - represents the target schema for storage.
/// Healthcare integration: design for HIPAA-aligned field naming and extensibility.
/// </summary>
public sealed class CanonicalRecord
{
    public string? ExternalId { get; set; }
    public string? PatientIdentifier { get; set; }
    public string? DocumentType { get; set; }
    public DateTime? DocumentDate { get; set; }
    public string? Description { get; set; }
    public string? SourceSystem { get; set; }
    public string? SourceFile { get; set; }
    public int SourceRowIndex { get; set; }
}
